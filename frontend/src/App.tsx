import CreateBookPage from './pages/CreateBookPage';
import { BookOpen } from 'lucide-react';

export default function App() {
  return <>
    <header className="site-header"><div className="header-inner"><a className="brand" href="/" aria-label="E-Kitap Oluşturucu ana sayfa"><span className="brand-icon"><BookOpen size={23} strokeWidth={1.6} /></span><span>E-Kitap<span className="brand-subtitle">OLUŞTURUCU</span></span></a><span className="header-tagline">Çalışmalarınızın yeni sayfası.</span><span className="edition-label">WORD → PDF</span></div></header>
    <main className="page"><CreateBookPage /></main>
    <footer className="site-footer"><span>E-Kitap Oluşturucu</span><span>Bilgiyi düzenleyin. Birlikte paylaşın.</span></footer>
  </>;
}
