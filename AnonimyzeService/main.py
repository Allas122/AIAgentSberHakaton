import os
import sys
import re
import signal
import logging
import threading
from concurrent import futures

import grpc
import torch
from transformers import AutoModelForTokenClassification, AutoTokenizer, pipeline
from dotenv import load_dotenv

generated_path = os.path.join(os.path.dirname(__file__), 'generated')
sys.path.insert(0, generated_path)
import generated.anonymize_service_pb2 as pb2
import generated.anonymize_service_pb2_grpc as pb2_grpc

logging.basicConfig(
    level=os.getenv("LOG_LEVEL") or "INFO",
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("pii_anonymizer")


def _env_int(name, default):
    raw = os.getenv(name)
    if not raw:
        return default
    try:
        return int(raw)
    except ValueError:
        logger.warning("Invalid %s=%r, falling back to %d", name, raw, default)
        return default


MAX_TEXT_LENGTH = _env_int("MAX_TEXT_LENGTH", 20000)

DEFAULT_REDACT_TYPES = {
    "GIVEN_NAME", "SURNAME", "DATE_OF_BIRTH",
    "EMAIL", "PHONE", "FAX_NUMBER",
    "STREET_ADDRESS", "STREET_NAME", "BUILDING_NUMBER", "SECONDARY_ADDRESS", "ZIP_CODE",
    "PASSPORT", "SSN", "TAX_ID", "GOVERNMENT_ID", "DRIVERS_LICENSE",
    "MEDICAL_RECORD_NUMBER", "EMPLOYEE_ID", "CUSTOMER_ID",
    "ACCOUNT_NUMBER", "IBAN", "SWIFT_BIC", "ROUTING_NUMBER",
    "CREDIT_DEBIT_CARD", "CVV", "PIN",
    "USERNAME", "PASSWORD", "API_KEY",
    "LICENSE_PLATE", "MAC_ADDRESS",
    "INN", "SNILS",
}

def _parse_types(raw):
    return {t.strip().upper() for t in raw.split(",") if t.strip()}

REDACT_TYPES = _parse_types(os.getenv("REDACT_TYPES") or ",".join(sorted(DEFAULT_REDACT_TYPES)))

MIN_SCORE_BY_TYPE = {
    "GIVEN_NAME": 0.6,
    "SURNAME": 0.6,
    "STREET_NAME": 0.6,
    "BUILDING_NUMBER": 0.7,
    "USERNAME": 0.7,
    "PASSWORD": 0.8,
    "API_KEY": 0.8,
    "PIN": 0.8,
}
DEFAULT_MIN_SCORE = 0.5

URL_PATTERN = re.compile(r"(?:https?://|www\.)\S+", re.IGNORECASE)
CREDENTIAL_TYPES = {"PASSWORD", "API_KEY", "USERNAME", "PIN", "CUSTOMER_ID", "EMPLOYEE_ID", "ACCOUNT_NUMBER"}

TRIM_CHARS = " \t\n\r\v\f«»\"'`“”„.,;:!?()[]{}<>-–—/\\|"

CHUNK_MAX_TOKENS = _env_int("CHUNK_MAX_TOKENS", 400)
CHUNK_OVERLAP_TOKENS = _env_int("CHUNK_OVERLAP_TOKENS", 50)
NER_BATCH_SIZE = _env_int("NER_BATCH_SIZE", 8)

class PIIAnonymizationEngine:
    def __init__(self):
        model_id = os.getenv("PII_MODEL_ID") or "Wismut/nym-pii-multilingual"
        model_revision = os.getenv("PII_MODEL_REVISION") or None
        device = 0 if torch.cuda.is_available() else -1

        logger.info("Loading model %s (revision=%s) on device=%s", model_id, model_revision, device)
        self.tokenizer = AutoTokenizer.from_pretrained(model_id, revision=model_revision)
        self.model = AutoModelForTokenClassification.from_pretrained(model_id, revision=model_revision)

        if self.tokenizer.pad_token is None:
            self.tokenizer.pad_token = self.tokenizer.eos_token or self.tokenizer.sep_token
        self.ner_pipeline = pipeline(
            "ner",
            model=self.model,
            tokenizer=self.tokenizer,
            aggregation_strategy="simple",
            device=device,
        )

        self._lock = threading.Lock()

        self.extra_patterns = {
            "INN": re.compile(r"\b\d{10}(\d{2})?\b"),
            "SNILS": re.compile(r"\b\d{3}[-.\s]?\d{3}[-.\s]?\d{3}[-.\s]?\d{2}\b"),
        }

    @staticmethod
    def _is_valid_inn(digits: str) -> bool:
        if not digits.isdigit():
            return False

        def checksum(coeffs, d):
            return (sum(c * int(x) for c, x in zip(coeffs, d)) % 11) % 10

        if len(digits) == 10:
            coeffs = [2, 4, 10, 3, 5, 9, 4, 6, 8]
            return checksum(coeffs, digits[:9]) == int(digits[9])
        if len(digits) == 12:
            coeffs1 = [7, 2, 4, 10, 3, 5, 9, 4, 6, 8]
            coeffs2 = [3, 7, 2, 4, 10, 3, 5, 9, 4, 6, 8]
            return (
                checksum(coeffs1, digits[:10]) == int(digits[10])
                and checksum(coeffs2, digits[:11]) == int(digits[11])
            )
        return False

    def _chunk_text(self, text):
        with self._lock:
            encoding = self.tokenizer(text, return_offsets_mapping=True, add_special_tokens=False)

        offsets = encoding["offset_mapping"]

        if len(offsets) <= CHUNK_MAX_TOKENS:
            return [(0, text)]

        chunks = []
        start_tok = 0
        n = len(offsets)
        while start_tok < n:
            end_tok = min(start_tok + CHUNK_MAX_TOKENS, n)
            char_start = offsets[start_tok][0]
            char_end = offsets[end_tok - 1][1]
            chunks.append((char_start, text[char_start:char_end]))
            if end_tok == n:
                break
            start_tok = end_tok - CHUNK_OVERLAP_TOKENS

        return chunks

    @staticmethod
    def _trim_span(text, start, end):
        """Отрезает пробелы и пунктуацию по краям и не даёт спану начаться или кончиться
        посреди слова. Возвращает None, если после подрезки не осталось ничего значимого."""
        while start < end and text[start] in TRIM_CHARS:
            start += 1
        while end > start and text[end - 1] in TRIM_CHARS:
            end -= 1

        if start >= end:
            return None

        while start > 0 and text[start - 1].isalnum() and text[start].isalnum():
            start -= 1
        while end < len(text) and text[end - 1].isalnum() and text[end].isalnum():
            end += 1

        if not any(ch.isalnum() for ch in text[start:end]):
            return None

        return start, end

    def _infer(self, chunks):
        texts = [text for _, text in chunks]

        try:
            with self._lock:
                return self.ner_pipeline(texts, batch_size=NER_BATCH_SIZE)
        except Exception:
            logger.exception("NER pipeline failed on batch of %d chunks, retrying one by one", len(texts))

        results = []
        for offset, text in chunks:
            try:
                with self._lock:
                    results.append(self.ner_pipeline(text))
            except Exception:
                logger.exception("NER pipeline failed on chunk at offset %d", offset)
                results.append([])

        return results

    def _run_ner(self, text):
        candidates = []
        url_spans = [(m.start(), m.end()) for m in URL_PATTERN.finditer(text)]

        chunks = [(offset, chunk) for offset, chunk in self._chunk_text(text) if chunk.strip()]
        if not chunks:
            return candidates

        for (chunk_offset, _), results in zip(chunks, self._infer(chunks)):
            for ent in results:
                etype = ent["entity_group"]
                if etype not in REDACT_TYPES:
                    continue

                score = float(ent.get("score", 1.0))
                if score < MIN_SCORE_BY_TYPE.get(etype, DEFAULT_MIN_SCORE):
                    continue

                span = self._trim_span(text, ent["start"] + chunk_offset, ent["end"] + chunk_offset)
                if span is None:
                    continue

                start, end = span

                if etype in CREDENTIAL_TYPES and any(
                    start >= url_start and end <= url_end for url_start, url_end in url_spans
                ):
                    continue

                candidates.append({
                    "start": start,
                    "end": end,
                    "type": etype,
                    "text": text[start:end],
                    "score": score,
                })
        return candidates

    @staticmethod
    def _is_valid_snils(digits: str) -> bool:
        if len(digits) != 11 or not digits.isdigit():
            return False

        total = sum(int(d) * (9 - i) for i, d in enumerate(digits[:9]))
        expected = 0 if total in (100, 101) else total if total < 100 else total % 101
        if expected in (100, 101):
            expected = 0

        return expected == int(digits[9:])

    def _run_regex(self, text):
        candidates = []
        for label, pattern in self.extra_patterns.items():
            if label not in REDACT_TYPES:
                continue
            for m in pattern.finditer(text):
                value = m.group()
                digits = re.sub(r"\D", "", value)
                if label == "INN" and not self._is_valid_inn(digits):
                    continue
                if label == "SNILS" and not self._is_valid_snils(digits):
                    continue
                candidates.append({
                    "start": m.start(),
                    "end": m.end(),
                    "type": label,
                    "text": value,
                    "score": 1.0,
                })
        return candidates

    def anonymize(self, text):
        if not text or not text.strip():
            return []

        candidates = self._run_ner(text) + self._run_regex(text)

        candidates.sort(key=lambda c: (-c["score"], c["start"], -(c["end"] - c["start"])))

        final_results = []
        occupied = []

        for c in candidates:
            if any(c["start"] < end and c["end"] > start for start, end in occupied):
                continue
            etype = c["type"]
            final_results.append({
                "start": c["start"],
                "end": c["end"],
                "type": etype,
                "text": c["text"],
            })
            occupied.append((c["start"], c["end"]))

        final_results.sort(key=lambda r: r["start"])
        return final_results

class NerServicer(pb2_grpc.NerServiceServicer):
    def __init__(self):
        self.engine = PIIAnonymizationEngine()

    def ExtractEntities(self, request, context):
        text = request.text

        if len(text) > MAX_TEXT_LENGTH:
            context.set_code(grpc.StatusCode.INVALID_ARGUMENT)
            context.set_details(f"Text exceeds maximum length of {MAX_TEXT_LENGTH} characters")
            return pb2.NerResponse()

        try:
            results = self.engine.anonymize(text)
        except Exception as e:
            logger.exception("Failed to process ExtractEntities request")
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(str(e))
            return pb2.NerResponse()

        response = pb2.NerResponse()
        for ent in results:
            e = response.entities.add()
            e.text = str(ent["text"])
            e.type = str(ent["type"])
            e.start = int(ent["start"])
            e.end = int(ent["end"])
        return response

def serve():
    load_dotenv()

    port = _env_int("PORT", 50051)
    max_workers = _env_int("GRPC_MAX_WORKERS", 10)
    max_message_mb = _env_int("GRPC_MAX_MESSAGE_MB", 50)

    server = grpc.server(
        futures.ThreadPoolExecutor(max_workers=max_workers),
        options=[
            ("grpc.max_send_message_length", max_message_mb * 1024 * 1024),
            ("grpc.max_receive_message_length", max_message_mb * 1024 * 1024),
        ],
    )

    try:
        servicer = NerServicer()
    except Exception:
        logger.exception("Failed to initialize PII anonymization engine")
        sys.exit(1)

    pb2_grpc.add_NerServiceServicer_to_server(servicer, server)
    server.add_insecure_port(f"[::]:{port}")
    server.start()
    logger.info("Server started on port: %s", port)

    def _graceful_shutdown(signum, frame):
        logger.info("Received signal %s, shutting down gracefully...", signum)
        stop_event = server.stop(grace=10)
        stop_event.wait(10)
        sys.exit(0)

    signal.signal(signal.SIGTERM, _graceful_shutdown)
    signal.signal(signal.SIGINT, _graceful_shutdown)

    server.wait_for_termination()

if __name__ == "__main__":
    serve()
