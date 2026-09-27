import { LoaderCircle } from 'lucide-react';

export default function GenerationStatus({ uploading }: { uploading: boolean }) {
  return <div className="processing" role="status" aria-live="polite">
    <LoaderCircle className="spinner" size={23} />
    <div><strong>{uploading ? 'Bildiriler yükleniyor…' : 'Kitabınız hazırlanıyor…'}</strong>
      <p>{uploading ? 'Orijinal Word dosyalarınız aktarılıyor.' : 'İletişim bilgileri temizleniyor, içindekiler ve PDF oluşturuluyor. Lütfen bu sayfayı açık tutun.'}</p></div>
  </div>;
}
