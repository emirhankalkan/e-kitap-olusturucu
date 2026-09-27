import { useEffect, useRef, useState } from 'react';
import { AlertCircle, ArrowRight, BookOpen, Check, FileCheck2, ListOrdered, ShieldCheck, Sparkles } from 'lucide-react';
import FileUpload from '../components/FileUpload';
import PaperList from '../components/PaperList';
import GenerationStatus from '../components/GenerationStatus';
import BookResultPage from './BookResultPage';
import { generateBook, getBook, uploadBook } from '../services/booksApi';
import type { Book, SelectedFile } from '../types/book';

export default function CreateBookPage() {
  const [name, setName] = useState('');
  const [files, setFiles] = useState<SelectedFile[]>([]);
  const [book, setBook] = useState<Book | null>(null);
  const [phase, setPhase] = useState<'idle' | 'loading' | 'uploading' | 'generating'>('idle');
  const [error, setError] = useState('');
  const [validation, setValidation] = useState({ name: '', files: '' });
  const nameInput = useRef<HTMLInputElement>(null);
  const busy = phase !== 'idle' || book?.status === 'Processing';

  useEffect(() => {
    const id = new URLSearchParams(window.location.search).get('book');
    if (!id) return;
    const controller = new AbortController();
    setPhase('loading');
    getBook(id, controller.signal).then(setBook).catch(err => {
      if (!controller.signal.aborted) setError(message(err));
    }).finally(() => { if (!controller.signal.aborted) setPhase('idle'); });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (book?.status !== 'Processing' || phase === 'generating') return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      try {
        const current = await getBook(book!.id, controller.signal);
        if (controller.signal.aborted) return;
        setBook(current);
        setError('');
        if (current.status === 'Processing') timer = setTimeout(poll, 2000);
      } catch (err) {
        if (!controller.signal.aborted) { setError(message(err)); timer = setTimeout(poll, 5000); }
      }
    }
    timer = setTimeout(poll, 1000);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [book?.id, book?.status, phase]);

  function addFiles(incoming: File[]) {
    setError('');
    setValidation(previous => ({ ...previous, files: '' }));
    if (files.length + incoming.length > 10) return setError('En fazla 10 bildiri ekleyebilirsiniz. Seçiminizi azaltıp tekrar deneyin.');
    const invalid = incoming.find(file => !file.name.toLowerCase().endsWith('.docx') || file.size === 0 || file.size > 10 * 1024 * 1024);
    if (invalid) return setError(`“${invalid.name}” eklenemedi. Boş olmayan, en fazla 10 MB boyutunda .docx dosyaları seçin.`);
    const keys = new Set(files.map(({ file }) => `${file.name}:${file.size}:${file.lastModified}`));
    for (const file of incoming) {
      const key = `${file.name}:${file.size}:${file.lastModified}`;
      if (keys.has(key)) return setError(`“${file.name}” zaten seçilmiş. Aynı dosyayı iki kez eklemeyin.`);
      keys.add(key);
    }
    setFiles(previous => [...previous, ...incoming.map(file => ({ id: crypto.randomUUID(), file }))]);
  }

  async function create() {
    if (busy) return;
    if (!book) {
      const nextValidation = {
        name: name.trim() ? '' : 'Devam etmek için kitap adını girin.',
        files: files.length === 10 ? '' : `Tam olarak 10 Word dosyası yükleyin. Şu anda ${files.length} dosya seçili.`,
      };
      setValidation(nextValidation);
      if (nextValidation.name || nextValidation.files) {
        if (nextValidation.name) nameInput.current?.focus();
        else document.getElementById('papers-error')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        return;
      }
    }
    setError('');
    let current = book;
    try {
      if (!current) {
        setPhase('uploading');
        current = await uploadBook(name, files.map(item => item.file));
        setBook(current);
        // Kitap kimliği, sayfa yenilendiğinde sonucu geri yüklemeyi sağlar.
        window.history.replaceState(null, '', `?book=${current.id}`);
      }
      setPhase('generating');
      setBook(await generateBook(current.id));
    } catch (err) {
      setError(message(err));
      if (current) {
        try { setBook(await getBook(current.id)); } catch { /* İlk hata mesajını koru. */ }
      }
    } finally { setPhase('idle'); }
  }

  function reset() {
    setBook(null); setFiles([]); setName(''); setError(''); setValidation({ name: '', files: '' });
    window.history.replaceState(null, '', window.location.pathname);
  }

  if (book?.status === 'Completed') return <BookResultPage book={book} onReset={reset} />;
  if (phase === 'loading') return <div className="panel loading-panel" role="status">Kitabınız getiriliyor…</div>;

  return <>
    <section className="hero">
      <div className="hero-title"><div className="eyebrow"><span /> BİLGİYİ BİR ARAYA GETİRİN</div><h1>Değerli çalışmalarınız,<br /><em>tek bir kitapta.</em></h1></div>
      <div className="hero-copy"><p>10 Word bildirisini düzenli, iletişim bilgilerinden arındırılmış bir PDF e-kitaba dönüştürün.</p>
      <div className="hero-details"><span><Check size={15} /> Otomatik içindekiler</span><span><Check size={15} /> Sayfa numaraları</span><span><Check size={15} /> Orijinal dosyalar korunur</span></div></div>
    </section>
    <div className="workspace-grid">
      <section className="panel editor-panel">
        <div className="section-heading"><span className="step-number">01</span><div><h2>Kitabınızı hazırlayın</h2><p>Bir isim verin, bildirilerinizi ekleyin.</p></div></div>
        {book ? <div className="saved-book"><span>KİTAP ADI</span><h3>{book.name}</h3><p>{book.papers.length} bildiri yüklendi. Orijinal dosyalarınız saklanıyor.</p></div> : <>
          <label className="input-label" htmlFor="book-name">Kitap adı <span>*</span></label>
          <input ref={nameInput} id="book-name" className={`name-input ${validation.name ? 'input-error' : ''}`} value={name} maxLength={200} disabled={busy} placeholder="Örn. Geleceğe Yön Veren Araştırmalar" onChange={event => { setName(event.target.value); if (event.target.value.trim()) setValidation(previous => ({ ...previous, name: '' })); }} aria-invalid={Boolean(validation.name)} aria-describedby={validation.name ? 'name-error' : 'name-help'} required />
          {validation.name && <p id="name-error" className="field-error" role="alert"><AlertCircle size={15} />{validation.name}</p>}
          <div className="input-help" id="name-help"><span>PDF’in içindekiler sayfasında görünecek.</span><span>{name.length}/200</span></div>
          <div className="upload-heading"><span>Bildiriler</span><span className={files.length === 10 ? 'count complete' : 'count'}>{files.length} / 10 dosya</span></div>
          <div className={validation.files ? 'upload-error' : ''}><FileUpload onAdd={addFiles} disabled={busy} count={files.length} /></div>
          {validation.files && <p id="papers-error" className="field-error" role="alert"><AlertCircle size={15} />{validation.files}</p>}
          <PaperList files={files} disabled={busy} onReorder={setFiles} onRemove={id => { setFiles(previous => previous.filter(file => file.id !== id)); setError(''); setValidation(previous => ({ ...previous, files: '' })); }} />
        </>}
        {(error || book?.errorMessage) && <div className="error-message" role="alert"><AlertCircle size={20} /><div><strong>İşlem tamamlanamadı</strong><p>{error || book?.errorMessage}</p></div></div>}
        {(phase === 'uploading' || phase === 'generating' || book?.status === 'Processing') && <GenerationStatus uploading={phase === 'uploading'} />}
        {book && !busy && <button className="text-button" onClick={reset}>Başka dosyalarla yeni kitap oluştur</button>}
      </section>
      <aside className="summary-column">
        <div className="book-preview" aria-hidden="true"><div className="book-cover"><div className="cover-top">BİLDİRİ KOLEKSİYONU <span>✦</span></div><div className="cover-title">{book?.name || name.trim() || <>Bilginin<br />bir araya<br /><i>geldiği yer.</i></>}</div><div className="cover-bottom"><span>10 BİLDİRİ · TEK KİTAP</span><BookOpen size={25} strokeWidth={1} /></div></div><span className="preview-caption">BİRLİKTE DAHA DEĞERLİ</span></div>
        <div className="summary-body"><div className="section-heading"><span className="step-number">02</span><h2>Kitaba dönüştürün</h2></div>
          <ul className="benefits"><li><ShieldCheck size={20} /><div><strong>İletişim bilgileri temizlenir</strong><p>E-posta ve telefonlar PDF’ten çıkarılır.</p></div></li><li><ListOrdered size={20} /><div><strong>İçindekiler otomatik hazırlanır</strong><p>Bildiri sırası ve sayfaları bir arada.</p></div></li><li><FileCheck2 size={20} /><div><strong>Tek, düzenli bir PDF</strong><p>Önizleyin, indirin ve paylaşın.</p></div></li></ul>
          {(validation.name || validation.files) && <div className="validation-summary" role="alert"><AlertCircle size={18} /><div><strong>Eksik bilgileri tamamlayın</strong><p>{[validation.name, validation.files].filter(Boolean).join(' ')}</p></div></div>}
          <button className="button primary generate-button" disabled={busy} onClick={create}><Sparkles size={18} />{busy ? 'İşlem devam ediyor…' : book?.status === 'Failed' ? 'Yeniden dene' : 'Kitabı oluştur'}{!busy && <ArrowRight size={18} />}</button>
          <p className="button-hint">{book ? 'Belgelerinizin orijinalleri değiştirilmez.' : files.length < 10 ? `Başlamak için kitap adı ve ${10 - files.length} bildiri${files.length ? ' daha' : ''} ekleyin.` : !name.trim() ? 'Son bir adım: kitabınıza bir isim verin.' : 'Her şey hazır. Kitabınızı oluşturabilirsiniz.'}</p>
        </div>
      </aside>
    </div>
    <div className="bottom-note"><ShieldCheck size={16} /><p>Temizlik yalnızca oluşturulan PDF’e uygulanır. Kaynak Word dosyalarınız olduğu gibi korunur.</p></div>
  </>;
}

function message(error: unknown) { return error instanceof Error ? error.message : 'Beklenmeyen bir hata oluştu. Yeniden deneyin.'; }
