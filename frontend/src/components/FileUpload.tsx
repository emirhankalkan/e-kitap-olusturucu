import { useRef, useState } from 'react';
import { Upload, Plus } from 'lucide-react';

interface Props { onAdd: (files: File[]) => void; disabled: boolean; count: number }

export default function FileUpload({ onAdd, disabled, count }: Props) {
  const input = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  return (
    <div className={`dropzone ${count > 0 ? 'is-compact' : ''} ${dragging ? 'is-dragging' : ''}`}
      onDragOver={event => { event.preventDefault(); if (!disabled) setDragging(true); }}
      onDragLeave={event => { if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setDragging(false); }}
      onDrop={event => { event.preventDefault(); setDragging(false); if (!disabled) onAdd(Array.from(event.dataTransfer.files)); }}>
      <div className="upload-symbol"><Upload size={count ? 20 : 25} strokeWidth={1.5} /></div>
      <div className="dropzone-copy"><h3>{count === 10 ? '10 bildiriniz hazır' : count ? `${10 - count} bildiri daha ekleyin` : 'Word dosyalarınızı buraya bırakın'}</h3>
      <p>{count === 10 ? 'Sıralamayı aşağıdaki tutamaçlardan değiştirebilirsiniz.' : count ? 'Dosyaları buraya bırakabilir veya seçebilirsiniz.' : 'veya bilgisayarınızdan seçerek ekleyin'}</p></div>
      <button type="button" className="button secondary" disabled={disabled || count === 10} onClick={() => input.current?.click()}>
        <Plus size={17} /> Dosya seç
      </button>
      <input ref={input} type="file" accept=".docx" multiple className="sr-only" tabIndex={-1} aria-label="Word bildirilerini seç"
        disabled={disabled || count === 10} onChange={event => { onAdd(Array.from(event.target.files ?? [])); event.target.value = ''; }} />
      <span className="file-hint">Yalnızca .docx · Tam 10 dosya · Dosya başına en fazla 10 MB</span>
    </div>
  );
}
