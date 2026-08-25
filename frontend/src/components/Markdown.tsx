import { useMemo } from 'react';

const escapeHtml = (raw: string) =>
  raw
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

function inline(text: string): string {
  return (
    text
      .replace(/`([^`\n]+)`/g, '<code>$1</code>')
      .replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>')
      .replace(/(^|[^*\w])\*([^*\n]+)\*(?![*\w])/g, '$1<em>$2</em>')
      .replace(/(^|[^_\w])_([^_\n]+)_(?![_\w])/g, '$1<em>$2</em>')
      .replace(/~~([^~\n]+)~~/g, '<s>$1</s>')
      .replace(
        /\[([^\]\n]+)\]\((https?:\/\/[^\s)]+)\)/g,
        '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>',
      )
      .replace(
        /(^|[\s(])((?:https?:\/\/)[^\s<)]+)/g,
        '$1<a href="$2" target="_blank" rel="noopener noreferrer">$2</a>',
      )
  );
}

function renderTable(rows: string[]): string {
  const cells = (line: string) =>
    line
      .replace(/^\s*\|/, '')
      .replace(/\|\s*$/, '')
      .split('|')
      .map((c) => c.trim());

  const [head, , ...body] = rows;
  const thead = `<tr>${cells(head)
    .map((c) => `<th>${inline(c)}</th>`)
    .join('')}</tr>`;
  const tbody = body
    .map((row) => `<tr>${cells(row).map((c) => `<td>${inline(c)}</td>`).join('')}</tr>`)
    .join('');
  return `<div class="md-table"><table><thead>${thead}</thead><tbody>${tbody}</tbody></table></div>`;
}

function toHtml(source: string): string {
  const lines = escapeHtml(source.replace(/\r\n/g, '\n')).split('\n');
  const out: string[] = [];
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    const fence = line.match(/^\s*```(\w+)?\s*$/);
    if (fence) {
      const buffer: string[] = [];
      i += 1;
      while (i < lines.length && !/^\s*```\s*$/.test(lines[i])) {
        buffer.push(lines[i]);
        i += 1;
      }
      i += 1;
      out.push(`<pre><code>${buffer.join('\n')}</code></pre>`);
      continue;
    }

    if (/^\s*\|.*\|\s*$/.test(line) && /^\s*\|[\s:|-]+\|\s*$/.test(lines[i + 1] ?? '')) {
      const rows: string[] = [];
      while (i < lines.length && /^\s*\|.*\|\s*$/.test(lines[i])) {
        rows.push(lines[i]);
        i += 1;
      }
      out.push(renderTable(rows));
      continue;
    }

    const heading = line.match(/^\s*(#{1,3})\s+(.*)$/);
    if (heading) {
      const level = heading[1].length;
      out.push(`<h${level}>${inline(heading[2])}</h${level}>`);
      i += 1;
      continue;
    }

    if (/^\s*([-*_])\1{2,}\s*$/.test(line)) {
      out.push('<hr />');
      i += 1;
      continue;
    }

    if (/^\s*&gt;\s?/.test(line)) {
      const buffer: string[] = [];
      while (i < lines.length && /^\s*&gt;\s?/.test(lines[i])) {
        buffer.push(lines[i].replace(/^\s*&gt;\s?/, ''));
        i += 1;
      }
      out.push(`<blockquote>${inline(buffer.join(' '))}</blockquote>`);
      continue;
    }

    const bullet = /^\s*[-*•]\s+(.*)$/;
    const ordered = /^\s*\d+[.)]\s+(.*)$/;
    if (bullet.test(line) || ordered.test(line)) {
      const isOrdered = ordered.test(line);
      const pattern = isOrdered ? ordered : bullet;
      const items: string[] = [];

      while (i < lines.length) {
        if (pattern.test(lines[i])) {
          items.push(`<li>${inline(lines[i].match(pattern)![1])}</li>`);
          i += 1;
          continue;
        }
        if (!lines[i].trim()) {
          let j = i;
          while (j < lines.length && !lines[j].trim()) j += 1;
          if (j < lines.length && pattern.test(lines[j])) {
            i = j;
            continue;
          }
        }
        break;
      }

      const tag = isOrdered ? 'ol' : 'ul';
      const first = Number(line.match(/^\s*(\d+)[.)]/)?.[1] ?? 1);
      const startAttr = isOrdered && first !== 1 ? ` start="${first}"` : '';
      out.push(`<${tag}${startAttr}>${items.join('')}</${tag}>`);
      continue;
    }

    if (!line.trim()) {
      i += 1;
      continue;
    }

    const paragraph: string[] = [];
    while (
      i < lines.length &&
      lines[i].trim() &&
      !/^\s*(#{1,3}\s|```|&gt;|[-*•]\s|\d+[.)]\s|\|)/.test(lines[i])
    ) {
      paragraph.push(lines[i]);
      i += 1;
    }
    if (paragraph.length) out.push(`<p>${inline(paragraph.join('<br />'))}</p>`);
  }

  return out.join('');
}

export function Markdown({ text }: { text: string }) {
  const html = useMemo(() => toHtml(text), [text]);
  return <div className="md" dangerouslySetInnerHTML={{ __html: html }} />;
}
