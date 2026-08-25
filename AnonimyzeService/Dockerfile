FROM python:3.11-slim

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    HF_HOME=/opt/models/hf \
    PORT=50051

WORKDIR /app

ARG PIP_INDEX_URL=https://pypi.org/simple
ARG PIP_EXTRA_INDEX_URL=https://download.pytorch.org/whl/cpu

COPY requirements.txt .
RUN pip install --no-cache-dir \
        --index-url $PIP_INDEX_URL \
        --extra-index-url $PIP_EXTRA_INDEX_URL \
        -r requirements.txt

# Модель кладётся в образ на этапе сборки и дальше живёт по фиксированному пути.
#
# Раньше она либо тянулась с huggingface.co при первом старте контейнера, либо её
# нужно было руками положить в том hf-cache. Первое означает, что без доступа к HF
# контейнер не станет healthy, а chatnode ждёт его через depends_on: service_healthy —
# то есть не поднимается вообще ничего. Второе не воспроизводится на новой машине:
# в репозитории нигде не записано, чем именно наполнять том.
#
# Теперь модель скачивается один раз на docker build и сохраняется через
# save_pretrained в /opt/models/pii-model. Рантайму сеть не нужна, тома не нужны.
ARG PII_MODEL_SOURCE=Wismut/nym-pii-multilingual
ARG PII_MODEL_REVISION=

ENV PII_MODEL_SOURCE=${PII_MODEL_SOURCE} \
    PII_MODEL_REVISION=${PII_MODEL_REVISION} \
    PII_MODEL_ID=/opt/models/pii-model

RUN python -c "\
import os; \
from transformers import AutoModelForTokenClassification, AutoTokenizer; \
source = os.environ['PII_MODEL_SOURCE']; \
revision = os.environ.get('PII_MODEL_REVISION') or None; \
target = os.environ['PII_MODEL_ID']; \
AutoTokenizer.from_pretrained(source, revision=revision).save_pretrained(target); \
AutoModelForTokenClassification.from_pretrained(source, revision=revision).save_pretrained(target); \
print('model baked into image:', source, '->', target)"

COPY . .

EXPOSE 50051

CMD ["python", "main.py"]
