import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Activity, ArrowDownLeft, ArrowRight, ArrowUpRight, Boxes, Check, ChevronRight, ClipboardList, Layers3, LockKeyhole, MapPin, Menu, PackageSearch, ShieldCheck, X } from 'lucide-react'

type DemoItem = { code: string; name: string; quantity: number; unit: string; minimum: number }
type DemoLocation = { id: string; name: string; path: string; items: DemoItem[] }

// Dados fictícios, isolados da API. A prévia nunca lê ou modifica estoque real.
const demoLocations: DemoLocation[] = [
  { id: 'deposito', name: 'Depósito central', path: 'Matriz / Depósito central', items: [
    { code: 'EPI-014', name: 'Luva nitrílica', quantity: 84, unit: 'cx', minimum: 30 },
    { code: 'MNT-028', name: 'Filtro hidráulico', quantity: 12, unit: 'un', minimum: 8 },
    { code: 'ELT-007', name: 'Cabo de rede', quantity: 46, unit: 'un', minimum: 20 },
  ] },
  { id: 'armario', name: 'Armário 02', path: 'Matriz / Sala técnica / Armário 02', items: [
    { code: 'EPI-014', name: 'Luva nitrílica', quantity: 18, unit: 'cx', minimum: 30 },
    { code: 'MNT-028', name: 'Filtro hidráulico', quantity: 11, unit: 'un', minimum: 8 },
    { code: 'ELT-007', name: 'Cabo de rede', quantity: 34, unit: 'un', minimum: 20 },
  ] },
  { id: 'gaveta', name: 'Gaveta 03', path: 'Matriz / Sala técnica / Armário 02 / Gaveta 03', items: [
    { code: 'FIX-019', name: 'Parafuso M6', quantity: 124, unit: 'un', minimum: 50 },
    { code: 'ELT-011', name: 'Conector RJ45', quantity: 7, unit: 'un', minimum: 15 },
  ] },
]

const navLinks = [
  { href: '#produto', label: 'Produto' },
  { href: '#operacao', label: 'Como funciona' },
  { href: '#controle', label: 'Controle' },
]

function DemoPreview() {
  const [locationId, setLocationId] = useState('armario')
  const [onlyLow, setOnlyLow] = useState(false)
  const [selectedCode, setSelectedCode] = useState('EPI-014')
  const location = demoLocations.find(item => item.id === locationId) ?? demoLocations[1]
  const visible = onlyLow ? location.items.filter(item => item.quantity < item.minimum) : location.items
  const selected = visible.find(item => item.code === selectedCode) ?? visible[0] ?? location.items[0]

  function selectLocation(id: string) {
    const next = demoLocations.find(item => item.id === id)
    if (!next) return
    setLocationId(id)
    setSelectedCode(next.items[0].code)
  }

  return <div className="lp-preview" aria-label="Prévia interativa do estoque">
    <div className="lp-preview-head">
      <div className="lp-preview-brand"><span className="lp-preview-mark"><Boxes size={18} aria-hidden="true" /></span><span>StorageManager <small> / visão do estoque</small></span></div>
      <span className="lp-demo-tag">Dados demonstrativos</span>
    </div>
    <div className="lp-preview-body">
      <div className="lp-preview-sidebar" aria-label="Locais da demonstração">
        <p className="lp-panel-label">Estrutura física</p>
        {demoLocations.map((item, index) => <button key={item.id} type="button" onClick={() => selectLocation(item.id)} aria-pressed={locationId === item.id} className={`lp-location ${locationId === item.id ? 'is-active' : ''}`} style={{ '--depth': index } as React.CSSProperties}>
          {index === 0 ? <Layers3 size={16} aria-hidden="true" /> : <MapPin size={16} aria-hidden="true" />}
          <span>{item.name}</span>
          <ChevronRight size={14} aria-hidden="true" />
        </button>)}
      </div>
      <div className="lp-preview-main">
        <div className="lp-preview-title-row"><div><p className="lp-breadcrumb">{location.path}</p><h3>{location.name}</h3></div><span className="lp-count">{location.items.length} produtos</span></div>
        <div className="lp-preview-toolbar"><p>Posições de estoque</p><button type="button" onClick={() => setOnlyLow(value => !value)} aria-pressed={onlyLow} className={`lp-filter ${onlyLow ? 'is-active' : ''}`}>Apenas estoque baixo</button></div>
        <div className="lp-item-list">
          {visible.length ? visible.map(item => <button key={item.code} type="button" onClick={() => setSelectedCode(item.code)} aria-pressed={selected.code === item.code} className={`lp-item ${selected.code === item.code ? 'is-selected' : ''}`}>
            <span className="lp-item-icon"><PackageSearch size={18} aria-hidden="true" /></span><span className="lp-item-name"><strong>{item.name}</strong><small>{item.code}</small></span>
            <span className="lp-item-amount"><strong>{item.quantity} <small>{item.unit}</small></strong><small className={item.quantity < item.minimum ? 'lp-status-low' : 'lp-status-ok'}>{item.quantity < item.minimum ? '▲ Baixo' : '● Regular'}</small></span>
          </button>) : <div className="lp-preview-empty"><Check size={18} aria-hidden="true" />Nenhum item abaixo do mínimo neste local.<button type="button" onClick={() => setOnlyLow(false)}>Mostrar todos</button></div>}
        </div>
        {visible.length > 0 && <div className="lp-detail" aria-live="polite"><div><span>Item selecionado</span><strong>{selected.name}</strong></div><div><span>Disponível neste local</span><strong>{selected.quantity} {selected.unit}</strong></div><div><span>Estoque mínimo</span><strong>{selected.minimum} {selected.unit}</strong></div></div>}
      </div>
    </div>
  </div>
}

export function LandingPage({ signedIn }: { signedIn: boolean }) {
  const [menuOpen, setMenuOpen] = useState(false)
  const menuButton = useRef<HTMLButtonElement>(null)
  const cta = signedIn ? '/dashboard' : '/login'
  const ctaLabel = signedIn ? 'Abrir painel' : 'Acessar sistema'

  useEffect(() => {
    if (!menuOpen) return
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setMenuOpen(false); menuButton.current?.focus() }
    }
    document.addEventListener('keydown', closeOnEscape)
    return () => document.removeEventListener('keydown', closeOnEscape)
  }, [menuOpen])

  return <div className="landing">
    <a className="lp-skip" href="#conteudo">Pular para o conteúdo</a>
    <header className="lp-header"><div className="lp-container lp-header-inner">
      <Link to="/" className="lp-logo" aria-label="StorageManager, início"><span className="lp-logo-icon"><Boxes size={20} strokeWidth={2.2} aria-hidden="true" /></span><span>Storage<span>Manager</span></span></Link>
      <nav className="lp-nav" aria-label="Navegação principal">{navLinks.map(link => <a key={link.href} href={link.href}>{link.label}</a>)}</nav>
      <div className="lp-header-actions"><Link to={cta} className="lp-header-cta">{ctaLabel}<ArrowRight size={16} aria-hidden="true" /></Link></div>
      <button ref={menuButton} type="button" className="lp-menu-toggle" aria-label={menuOpen ? 'Fechar menu' : 'Abrir menu'} aria-expanded={menuOpen} aria-controls="lp-mobile-nav" onClick={() => setMenuOpen(value => !value)}>{menuOpen ? <X size={22} /> : <Menu size={22} />}</button>
    </div>{menuOpen && <nav id="lp-mobile-nav" className="lp-mobile-nav" aria-label="Navegação móvel">{navLinks.map(link => <a key={link.href} href={link.href} onClick={() => setMenuOpen(false)}>{link.label}</a>)}<Link to={cta} onClick={() => setMenuOpen(false)}>{ctaLabel}<ArrowRight size={17} aria-hidden="true" /></Link></nav>}</header>

    <main id="conteudo">
      <section className="lp-hero lp-container" aria-labelledby="lp-title"><div className="lp-hero-copy">
        <p className="lp-overline"><span aria-hidden="true" /> Clareza para cada movimentação</p>
        <h1 id="lp-title">Saiba o que tem.<br /><em>Encontre onde está.</em></h1>
        <p className="lp-hero-lead">Organize locais, acompanhe saldos e registre entradas e consumo em um só lugar. Sua equipe encontra a informação certa antes de agir.</p>
        <div className="lp-hero-actions"><Link to={cta} className="lp-button-primary">{ctaLabel}<ArrowRight size={19} aria-hidden="true" /></Link><a href="#produto" className="lp-button-text">Conhecer o produto<ChevronRight size={17} aria-hidden="true" /></a></div>
      </div><div className="lp-hero-visual"><DemoPreview /></div></section>

      <section id="produto" className="lp-product lp-container" aria-labelledby="lp-product-title"><div className="lp-section-intro"><p className="lp-kicker">Uma operação mais legível</p><h2 id="lp-product-title">O estoque faz sentido quando cada informação tem seu lugar.</h2><p>Do endereço físico ao histórico de quem movimentou, o StorageManager conecta as decisões do dia a dia.</p></div>
        <div className="lp-capabilities">
          <article className="lp-capability lp-capability-wide"><div className="lp-capability-copy"><span className="lp-feature-icon"><MapPin size={22} aria-hidden="true" /></span><h3>Um mapa fiel dos seus locais</h3><p>Monte a estrutura que existe na sua operação: salas, armários, prateleiras e gavetas. Cada item fica associado a um endereço claro.</p></div><div className="lp-location-map" aria-label="Exemplo de hierarquia de locais"><span>Matriz</span><i aria-hidden="true" /><span>Sala técnica</span><i aria-hidden="true" /><span>Armário 02</span><i aria-hidden="true" /><strong>Gaveta 03 <MapPin size={15} aria-hidden="true" /></strong></div></article>
          <article className="lp-capability lp-capability-balance"><span className="lp-feature-icon"><PackageSearch size={22} aria-hidden="true" /></span><h3>Saldo que orienta a próxima ação</h3><p>Consulte quantidade por produto e local. Veja quando uma posição está abaixo do mínimo definido.</p><div className="lp-balance-example"><div><span>Filtro hidráulico</span><strong>11 <small>un</small></strong></div><div><span>Mínimo</span><strong>8 <small>un</small></strong></div><p><span aria-hidden="true">●</span> Regular neste local</p></div></article>
          <article className="lp-capability lp-capability-history"><span className="lp-feature-icon"><ClipboardList size={22} aria-hidden="true" /></span><h3>Histórico que pode ser conferido</h3><p>Entradas e consumo ficam registrados com produto, local, responsável e data. A consulta acompanha a operação.</p><div className="lp-history-example"><span><ArrowDownLeft size={17} aria-hidden="true" /> Entrada registrada</span><span><ArrowUpRight size={17} aria-hidden="true" /> Consumo registrado</span></div></article>
        </div><p className="lp-demo-note">As informações exibidas nesta página são exemplos ilustrativos. A prévia não acessa dados da sua organização.</p>
      </section>

      <section id="operacao" className="lp-operation" aria-labelledby="lp-operation-title"><div className="lp-container lp-operation-inner"><div className="lp-operation-copy"><h2 id="lp-operation-title">Da chegada ao uso, sem perder o contexto.</h2><p>O fluxo acompanha o que a equipe realmente faz. Cadastre o produto, indique onde ele foi guardado e registre cada saída no momento certo.</p><Link to={cta} className="lp-inline-link">{ctaLabel}<ArrowRight size={18} aria-hidden="true" /></Link></div><ol className="lp-flow"><li><span className="lp-flow-icon"><Boxes size={22} aria-hidden="true" /></span><div><strong>Cadastre</strong><p>Identifique produtos, unidades e estoque mínimo.</p></div></li><li><span className="lp-flow-icon"><ArrowDownLeft size={22} aria-hidden="true" /></span><div><strong>Receba</strong><p>Registre a entrada no local onde o material foi guardado.</p></div></li><li><span className="lp-flow-icon"><ArrowUpRight size={22} aria-hidden="true" /></span><div><strong>Movimente</strong><p>Informe a quantidade consumida e quem recebeu o material.</p></div></li><li><span className="lp-flow-icon"><Activity size={22} aria-hidden="true" /></span><div><strong>Consulte</strong><p>Encontre o saldo atual e confira o histórico de movimentações.</p></div></li></ol></div></section>

      <section id="controle" className="lp-control lp-container" aria-labelledby="lp-control-title"><div className="lp-control-visual" aria-hidden="true"><div className="lp-control-window"><div className="lp-control-window-head"><LockKeyhole size={18} /> Acesso por função</div><div className="lp-role"><span>Administrador</span><strong>Gerencia usuários e operação <Check size={16} /></strong></div><div className="lp-role"><span>Operador</span><strong>Registra movimentações <Check size={16} /></strong></div><div className="lp-role"><span>Consulta</span><strong>Acompanha sem alterar <Check size={16} /></strong></div></div></div><div className="lp-control-copy"><span className="lp-feature-icon"><ShieldCheck size={23} aria-hidden="true" /></span><h2 id="lp-control-title">Cada pessoa acessa o que precisa para trabalhar.</h2><p>Perfis de administrador, operador e consulta ajudam a manter a rotina organizada. Ações ficam vinculadas a quem as realizou.</p><p className="lp-control-small">Uma instalação pode funcionar na rede local ou ser publicada com HTTPS. A disponibilidade depende da infraestrutura escolhida.</p></div></section>

      <section className="lp-final" aria-labelledby="lp-final-title"><div className="lp-container lp-final-inner"><div><p>Seu próximo movimento começa com clareza.</p><h2 id="lp-final-title">Organize o estoque que a sua equipe usa todos os dias.</h2></div><Link to={cta} className="lp-button-primary">{ctaLabel}<ArrowRight size={19} aria-hidden="true" /></Link></div></section>
    </main>
    <footer className="lp-footer"><div className="lp-container lp-footer-inner"><Link to="/" className="lp-logo" aria-label="StorageManager, início"><span className="lp-logo-icon"><Boxes size={18} aria-hidden="true" /></span><span>Storage<span>Manager</span></span></Link><p>Gestão de estoque para equipes que precisam saber onde cada item está.</p><Link to={cta}>{ctaLabel}<ArrowRight size={16} aria-hidden="true" /></Link></div></footer>
  </div>
}
