import { Link, NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { Activity, Boxes, Building2, LayoutDashboard, LogOut, Menu, PackageSearch, Users, X } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '@/auth'
import { AuthPage } from '@/pages/auth-page'
import { LandingPage } from '@/pages/landing-page'
import { DashboardPage, LocationsPage, MovementsPage, ProductsPage, StockPage, UsersPage } from '@/pages/app-pages'
import { ErrorNotice, Loading } from '@/components/common'
import { Button } from '@/components/ui/button'

const navigation = [
  { to: '/dashboard', label: 'Visão geral', icon: LayoutDashboard },
  { to: '/stock', label: 'Estoque', icon: PackageSearch },
  { to: '/movements', label: 'Movimentações', icon: Activity },
  { to: '/products', label: 'Produtos', icon: Boxes },
  { to: '/locations', label: 'Locais', icon: Building2 },
]

export function App() {
  const auth = useAuth()
  const location = useLocation()
  const [menu, setMenu] = useState(false)
  if (location.pathname === '/') return <LandingPage signedIn={Boolean(auth.user)} />
  if (auth.loading) return <Loading label="Verificando sua sessão…" />
  if (auth.error) return <div className="mx-auto max-w-xl p-8"><ErrorNotice error={auth.error} retry={() => void auth.refresh()} /><Link to="/" className="mt-5 inline-block text-sm font-semibold text-primary hover:underline">Voltar ao início</Link></div>
  if (!auth.user) return location.pathname === '/login' ? <AuthPage /> : <Navigate to="/login" replace />
  if (location.pathname === '/login') return <Navigate to="/dashboard" replace />
  const links = auth.user.role === 'Admin' ? [...navigation, { to: '/users', label: 'Usuários', icon: Users }] : navigation
  return <div className="min-h-svh bg-background lg:grid lg:grid-cols-[248px_1fr]">
    {menu && <button aria-label="Fechar menu" className="fixed inset-0 z-40 bg-slate-950/35 lg:hidden" onClick={() => setMenu(false)} />}
    <aside className={`fixed inset-y-0 left-0 z-50 flex w-[270px] flex-col bg-[#152649] text-white transition-transform lg:sticky lg:top-0 lg:h-svh lg:w-auto ${menu ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}`}>
      <div className="flex h-20 items-center gap-3 border-b border-white/8 px-6 text-lg font-semibold"><span className="rounded-xl bg-white/10 p-2"><Boxes className="size-5" /></span>StorageManager<Button aria-label="Fechar menu" variant="ghost" size="icon" className="ml-auto text-white hover:bg-white/10 lg:hidden" onClick={() => setMenu(false)}><X className="size-5" /></Button></div>
      <nav className="flex-1 space-y-1 px-3 py-6" aria-label="Navegação principal">{links.map(item => <NavLink key={item.to} to={item.to} onClick={() => setMenu(false)} className={({ isActive }) => `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition ${isActive ? 'bg-white text-[#152649] shadow-sm' : 'text-slate-300 hover:bg-white/8 hover:text-white'}`}><item.icon className="size-[18px]" />{item.label}</NavLink>)}</nav>
      <div className="border-t border-white/8 p-4"><div className="mb-3 min-w-0 px-2"><p className="truncate text-sm font-medium">{auth.user.name}</p><p className="mt-0.5 truncate text-xs text-slate-400">{auth.user.organizationName}</p></div><Button variant="ghost" className="w-full justify-start text-slate-300 hover:bg-white/8 hover:text-white" onClick={() => void auth.logout()}><LogOut className="size-4" />Sair</Button></div>
    </aside>
    <main className="min-w-0"><header className="sticky top-0 z-30 flex h-16 items-center border-b bg-white/90 px-4 backdrop-blur lg:hidden"><Button variant="ghost" size="icon" onClick={() => setMenu(true)} aria-label="Abrir menu"><Menu className="size-5" /></Button><span className="ml-3 font-semibold">StorageManager</span></header><div className="mx-auto max-w-[1480px] px-4 py-7 sm:px-7 lg:px-10 lg:py-9"><Routes><Route path="/dashboard" element={<DashboardPage />} /><Route path="/stock" element={<StockPage />} /><Route path="/movements" element={<MovementsPage />} /><Route path="/products" element={<ProductsPage />} /><Route path="/locations" element={<LocationsPage />} />{auth.user.role === 'Admin' && <Route path="/users" element={<UsersPage />} />}<Route path="*" element={<Navigate to="/dashboard" replace />} /></Routes></div></main>
  </div>
}
