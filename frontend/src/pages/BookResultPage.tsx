import { ArrowLeft, Check, Download, FileText } from 'lucide-react';
import type { Book } from '../types/book';
import PdfViewer from '../components/PdfViewer';

export default function BookResultPage({ book, onReset }: { book: Book; onReset: () => void }) {
  return <>
    <div className="result-heading">
      <div><span className="success-label"><Check size={15} /> KİTABINIZ HAZIR</span><h1>{book.name}</h1><p>10 bildiri, tek bir kitap. Paylaşmaya hazır.</p></div>
      <a className="button primary" href={book.downloadUrl!}><Download size={18} /> PDF’i indir</a>
    </div>
    <div className="result-layout">
      <aside className="panel contents-panel"><div className="section-heading"><span className="step-number"><FileText size={17} /></span><h2>İçindekiler</h2></div>
        <ol>{book.papers.map(paper => <li key={paper.id}><span>{String(paper.sortOrder).padStart(2, '0')}</span><p>{paper.title || paper.fileName}</p><span className="page-number">{paper.startPage}</span></li>)}</ol>
        <button className="button secondary w-full" onClick={onReset}><ArrowLeft size={16} /> Yeni kitap oluştur</button>
      </aside>
      <PdfViewer url={book.pdfUrl!} />
    </div>
  </>;
}
