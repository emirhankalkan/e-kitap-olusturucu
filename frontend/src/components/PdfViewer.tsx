import { ExternalLink } from 'lucide-react';

export default function PdfViewer({ url }: { url: string }) {
  return <section className="pdf-panel" aria-label="PDF önizleme">
    <div className="pdf-toolbar"><span><span className="live-dot" /> PDF önizleme</span>
      <a href={url} target="_blank" rel="noopener noreferrer">Yeni sekmede aç <ExternalLink size={14} /></a></div>
    <iframe src={url} title="Oluşturulan e-kitabın PDF önizlemesi" />
    <p className="preview-help">Önizleme görünmüyorsa PDF’i yeni sekmede açabilir veya indirebilirsiniz.</p>
  </section>;
}
