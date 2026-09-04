import { useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react';
import {
  createUserWithEmailAndPassword,
  onAuthStateChanged,
  signInWithEmailAndPassword,
  signOut,
  type User,
} from 'firebase/auth';
import {
  addDoc,
  collection,
  doc,
  limit,
  onSnapshot,
  orderBy,
  query,
  serverTimestamp,
  setDoc,
} from 'firebase/firestore';
import {
  Activity,
  BarChart3,
  BookOpen,
  CheckCircle2,
  CircleDollarSign,
  Clock3,
  LayoutDashboard,
  LogOut,
  Menu,
  Settings,
  ShieldCheck,
  TrendingUp,
  UserRound,
  Wifi,
  WifiOff,
} from 'lucide-react';
import { auth, db } from '@/lib/firebase';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';

type Page = 'overview' | 'trade' | 'orders' | 'portfolio' | 'activity' | 'settings';
type ConnectionStatus = 'disconnected' | 'connecting' | 'unauthenticated' | 'connected';
type OrderSide = 'Buy' | 'Sell';
type OrderType = 'Market' | 'Limit';
type TimeInForce = 'GTC' | 'IOC' | 'FOK';

interface OrderRequest {
  OrderId: string;
  ClientId: string;
  Symbol: string;
  Quantity: number;
  Price: number;
  Side: OrderSide;
  OrderType: OrderType;
  TimeInForce: TimeInForce;
  Timestamp: string;
}

interface OrderResponse {
  OrderId: string;
  Status: string;
  ExecutedPrice: number;
  Message: string;
  ExecutedQuantity: number;
}

interface ActivityItem {
  id: string;
  type: string;
  message: string;
  status?: string;
  createdAt?: { toDate?: () => Date };
}

const pageMeta: Record<Page, { label: string; icon: typeof LayoutDashboard }> = {
  overview: { label: 'Overview', icon: LayoutDashboard },
  trade: { label: 'Trade', icon: TrendingUp },
  orders: { label: 'Orders', icon: BookOpen },
  portfolio: { label: 'Portfolio', icon: BarChart3 },
  activity: { label: 'Activity', icon: Activity },
  settings: { label: 'Settings', icon: Settings },
};

const defaultOrder = {
  symbol: 'AAPL',
  quantity: 1,
  price: 175.5,
  side: 'Buy' as OrderSide,
  type: 'Limit' as OrderType,
  tif: 'GTC' as TimeInForce,
};

function formatMoney(value: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format(value);
}

function statusVariant(status: ConnectionStatus) {
  if (status === 'connected') return 'default' as const;
  if (status === 'connecting' || status === 'unauthenticated') return 'secondary' as const;
  return 'destructive' as const;
}

export default function App() {
  const [user, setUser] = useState<User | null>(null);
  const [page, setPage] = useState<Page>(() => {
    const requested = window.location.hash.replace('#/', '') as Page;
    return requested in pageMeta ? requested : 'overview';
  });
  const [menuOpen, setMenuOpen] = useState(false);
  const [status, setStatus] = useState<ConnectionStatus>('disconnected');
  const [socket, setSocket] = useState<WebSocket | null>(null);
  const [authMode, setAuthMode] = useState<'signIn' | 'signUp'>('signIn');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [authError, setAuthError] = useState('');
  const [order, setOrder] = useState(defaultOrder);
  const [orders, setOrders] = useState<OrderResponse[]>([]);
  const [activity, setActivity] = useState<ActivityItem[]>([]);
  const [livePrice, setLivePrice] = useState(175.5);
  const [notice, setNotice] = useState('');

  const addActivity = async (message: string, type = 'info', response?: OrderResponse) => {
    const item = { type, message, status: response?.Status ?? '', createdAt: serverTimestamp() };
    setActivity((previous) => [{ id: crypto.randomUUID(), ...item }, ...previous].slice(0, 50) as ActivityItem[]);
    if (user) await addDoc(collection(db, 'users', user.uid, 'activity'), item);
  };

  useEffect(() => {
    const unsubscribe = onAuthStateChanged(auth, setUser);
    return () => unsubscribe();
  }, []);

  useEffect(() => {
    window.location.hash = `/${page}`;
    setMenuOpen(false);
  }, [page]);

  useEffect(() => {
    if (!user) {
      setActivity([]);
      return;
    }
    const activityQuery = query(
      collection(db, 'users', user.uid, 'activity'),
      orderBy('createdAt', 'desc'),
      limit(50),
    );
    return onSnapshot(activityQuery, (snapshot) => {
      setActivity(snapshot.docs.map((item) => ({ id: item.id, ...item.data() }) as ActivityItem));
    });
  }, [user]);

  useEffect(() => () => socket?.close(), [socket]);

  const authenticate = async () => {
    setAuthError('');
    try {
      const credential = authMode === 'signIn'
        ? await signInWithEmailAndPassword(auth, email, password)
        : await createUserWithEmailAndPassword(auth, email, password);
      await setDoc(doc(db, 'users', credential.user.uid), {
        email: credential.user.email,
        updatedAt: serverTimestamp(),
      }, { merge: true });
      await addActivity(authMode === 'signIn' ? 'Signed in to trading workspace.' : 'Created trading workspace account.', 'success');
    } catch (error) {
      setAuthError(error instanceof Error ? error.message.replace('Firebase: ', '') : 'Authentication failed.');
    }
  };

  const connect = () => {
    if (!user || socket) return;
    setStatus('connecting');
    const configuredUrl = import.meta.env.VITE_WS_URL as string | undefined;
    const wsUrl = configuredUrl ?? 'wss://fluffy-octo-engine-production.up.railway.app/ws';
    const nextSocket = new WebSocket(wsUrl);
    nextSocket.onopen = () => {
      setStatus('unauthenticated');
      void addActivity('Connected to Railway trading gateway.');
    };
    nextSocket.onmessage = async (event) => {
      if (event.data.includes('Firebase ID token')) {
        nextSocket.send(await user.getIdToken());
        return;
      }
      if (event.data.includes('Authenticated')) {
        setStatus('connected');
        void addActivity('Firebase session authenticated by trading gateway.', 'success');
        return;
      }
      if (event.data.includes('Authentication failed')) {
        setStatus('disconnected');
        void addActivity('Gateway rejected the Firebase ID token. Check Railway Firebase Admin configuration.', 'error');
        nextSocket.close();
        return;
      }
      try {
        const response = JSON.parse(event.data) as OrderResponse;
        setOrders((previous) => [response, ...previous].slice(0, 50));
        void addActivity(`Order ${response.OrderId}: ${response.Status} — ${response.Message}`, response.Status === 'Rejected' ? 'error' : 'success', response);
      } catch {
        void addActivity(`Gateway: ${event.data}`);
      }
    };
    nextSocket.onerror = () => void addActivity('WebSocket connection error.', 'error');
    nextSocket.onclose = () => {
      setSocket(null);
      setStatus('disconnected');
      void addActivity('Disconnected from Railway trading gateway.', 'error');
    };
    setSocket(nextSocket);
  };

  const disconnect = () => {
    socket?.close();
    setSocket(null);
    setStatus('disconnected');
  };

  const submitOrder = () => {
    if (!socket || status !== 'connected' || !user) return;
    const request: OrderRequest = {
      OrderId: crypto.randomUUID(),
      ClientId: user.uid,
      Symbol: order.symbol.trim().toUpperCase(),
      Quantity: order.quantity,
      Price: order.type === 'Market' ? 0 : order.price,
      Side: order.side,
      OrderType: order.type,
      TimeInForce: order.tif,
      Timestamp: new Date().toISOString(),
    };
    socket.send(JSON.stringify(request));
    void addActivity(`Submitted ${request.Side} ${request.Quantity} ${request.Symbol}.`);
    setNotice(`Order ${request.OrderId.slice(0, 8)} submitted to the matching engine.`);
  };

  const estimatedValue = useMemo(() => order.quantity * (order.type === 'Market' ? livePrice : order.price), [order, livePrice]);

  if (!user) {
    return (
      <div className="auth-page">
        <div className="brand" style={{ position: 'absolute', top: '40px', left: '40px' }}>
          <div className="brand-mark">FO</div>
          <div>
            <strong style={{ color: 'white', fontSize: '18px', letterSpacing: '-0.02em' }}>Fluffy Octo</strong>
            <span style={{ display: 'block', color: 'var(--text-muted)', fontSize: '12px' }}>Trading Terminal</span>
          </div>
        </div>
        <Card className="auth-card">
          <CardHeader>
            <p className="eyebrow">Terminal Access</p>
            <CardTitle style={{ fontSize: '24px', letterSpacing: '-0.03em' }}>{authMode === 'signIn' ? 'Authenticated Login' : 'Initialize Workspace'}</CardTitle>
            <p className="muted" style={{ color: 'var(--text-muted)' }}>Identity verified via Firebase. Execution routed through Railway.</p>
          </CardHeader>
          <CardContent className="space-y-4">
            <Input type="email" placeholder="Operator Email" value={email} onChange={(event) => setEmail(event.target.value)} className="mono" />
            <Input type="password" placeholder="Passcode" value={password} onChange={(event) => setPassword(event.target.value)} className="mono" />
            {authError && <p className="error-text" style={{ color: 'var(--accent-red)', fontSize: '12px' }}>{authError}</p>}
            <Button className="w-full" onClick={authenticate} style={{ backgroundColor: 'var(--accent-cyan)', color: 'var(--bg-deep)', fontWeight: '700' }}>
              {authMode === 'signIn' ? 'Establish Session' : 'Create Identity'}
            </Button>
            <Button variant="ghost" className="w-full" onClick={() => setAuthMode(authMode === 'signIn' ? 'signUp' : 'signIn')}>
              {authMode === 'signIn' ? 'Request new account' : 'Return to login'}
            </Button>
          </CardContent>
        </Card>
        <p className="auth-footnote" style={{ color: 'var(--text-muted)', fontSize: '11px', marginTop: '20px' }}>
          <ShieldCheck className="inline h-3 w-3 mr-1" /> AES-256 Encryption · Railway High-Frequency Gateway
        </p>
      </div>
    );
  }

  return (
    <div className="app-shell">
      {menuOpen && <button className="mobile-backdrop" aria-label="Close menu" onClick={() => setMenuOpen(false)} />}
      <aside className={cn('sidebar', menuOpen && 'sidebar-open')}>
        <div className="brand">
          <div className="brand-mark">FO</div>
          <div>
            <strong style={{ color: 'white', fontSize: '16px', letterSpacing: '-0.02em' }}>Fluffy Octo</strong>
            <span style={{ display: 'block', color: 'var(--text-muted)', fontSize: '11px' }}>Trading Terminal</span>
          </div>
        </div>
        <nav className="nav-list">
          {(Object.keys(pageMeta) as Page[]).map((key) => {
            const Icon = pageMeta[key].icon;
            return <button key={key} className={cn('nav-item', page === key && 'nav-item-active')} onClick={() => setPage(key)}><Icon className="h-4 w-4" />{pageMeta[key].label}</button>;
          })}
        </nav>
        <div className="sidebar-bottom" style={{ marginTop: 'auto', paddingTop: '20px', borderTop: '1px solid var(--border-color)', display: 'grid', gap: '12px' }}>
          <div className="connection-mini" style={{ display: 'flex', alignItems: 'center', gap: '10px', padding: '0' }}>
            <span className={cn('status-dot', status === 'connected' && 'status-dot-live')} />
            <div style={{ fontSize: '12px' }}>
              <strong style={{ color: 'white', display: 'block' }}>{status === 'connected' ? 'Gateway Live' : 'Gateway Offline'}</strong>
              <span style={{ color: 'var(--text-muted)', fontSize: '10px' }}>Railway WebSocket</span>
            </div>
          </div>
          <button className="nav-item" onClick={() => void signOut(auth)}><LogOut className="h-4 w-4" />Sign out</button>
        </div>
      </aside>

      <main className="main-content">
        {/* Pulse Line Signature */}
        <div style={{ position: 'fixed', top: 0, left: 0, width: '100%', height: '2px', background: 'var(--border-color)', zIndex: 100 }}>
          <div style={{
            width: status === 'connected' ? '100%' : '0%',
            height: '100%',
            background: 'var(--accent-cyan)',
            transition: 'width 0.5s ease',
            boxShadow: '0 0 10px var(--accent-cyan)'
          }} />
        </div>

        <header className="topbar">
          <Button variant="ghost" className="mobile-menu" onClick={() => setMenuOpen(true)}><Menu className="h-5 w-5" /></Button>
          <div>
            <p className="eyebrow">Trading Workspace</p>
            <h1 style={{ fontSize: '28px', fontWeight: '800', letterSpacing: '-0.04em' }}>{pageMeta[page].label}</h1>
          </div>
          <div className="topbar-actions">
            <Badge variant={statusVariant(status)} style={{ border: '1px solid var(--border-color)', backgroundColor: 'var(--bg-surface)', color: status === 'connected' ? 'var(--accent-green)' : 'var(--text-muted)' }}>
              <span className={cn('mr-2 inline-block h-2 w-2 rounded-full bg-current', status === 'connected' && 'animate-pulse')} />
              {status}
            </Badge>
            <div className="user-chip" style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-muted)', fontSize: '12px', background: 'var(--bg-surface)', padding: '4px 12px', borderRadius: '20px', border: '1px solid var(--border-color)' }}>
              <UserRound className="h-3 w-3" />
              <span className="mono">{user.email}</span>
            </div>
          </div>
        </header>

        {page === 'overview' && <Overview user={user} status={status} orders={orders} activity={activity} livePrice={livePrice} onConnect={connect} onTrade={() => setPage('trade')} />}
        {page === 'trade' && <TradePanel order={order} setOrder={setOrder} estimatedValue={estimatedValue} livePrice={livePrice} onSubmit={submitOrder} connected={status === 'connected'} notice={notice} />}
        {page === 'orders' && <OrdersPanel orders={orders} />}
        {page === 'portfolio' && <PortfolioPanel livePrice={livePrice} setLivePrice={setLivePrice} orders={orders} />}
        {page === 'activity' && <ActivityPanel activity={activity} onClear={() => setActivity([])} />}
        {page === 'settings' && <SettingsPanel user={user} status={status} onConnect={connect} onDisconnect={disconnect} />}
      </main>
    </div>
  );
}

function Overview({ user, status, orders, activity, livePrice, onConnect, onTrade }: { user: User; status: ConnectionStatus; orders: OrderResponse[]; activity: ActivityItem[]; livePrice: number; onConnect: () => void; onTrade: () => void }) {
  const filled = orders.filter((order) => order.Status === 'Executed' || order.Status === 'PartiallyFilled').length;
  return <div className="page-stack">
    <section className="hero-card">
      <div>
        <p className="eyebrow">Welcome back</p>
        <h2 style={{ fontSize: '32px', fontWeight: '800', letterSpacing: '-0.04em', marginBottom: '12px' }}>Trade with precision, {user.email?.split('@')[0]}.</h2>
        <p style={{ color: 'var(--text-muted)', fontSize: '14px', lineHeight: '1.6', marginBottom: '24px' }}>
          Monitor your simulated market feeds and manage order execution via the Railway matching engine.
        </p>
        <div className="hero-actions" style={{ display: 'flex', gap: '12px' }}>
          <Button onClick={onTrade} style={{ backgroundColor: 'var(--accent-cyan)', color: 'var(--bg-deep)', fontWeight: '700' }}><TrendingUp className="mr-2 h-4 w-4" />Open Trade Ticket</Button>
          {status !== 'connected' && <Button variant="outline" onClick={onConnect} style={{ borderColor: 'var(--border-color)', color: 'white' }}><Wifi className="mr-2 h-4 w-4" />Connect Gateway</Button>}
        </div>
      </div>
      <div className="hero-orb">
        <TrendingUp className="h-6 w-6" style={{ color: 'var(--accent-cyan)' }} />
        <span style={{ fontSize: '10px', color: 'var(--text-muted)', textTransform: 'uppercase', marginTop: '8px' }}>AAPL / Mark</span>
        <strong style={{ fontSize: '24px', color: 'white', margin: '4px 0', fontFamily: 'JetBrains Mono' }}>{formatMoney(livePrice)}</strong>
        <small style={{ fontSize: '9px', color: 'var(--text-muted)' }}>Live Simulated Feed</small>
      </div>
    </section>
    <div className="metric-grid">
      <Metric label="Available Cash" value="$100,000.00" detail="Buying Power" icon={CircleDollarSign} />
      <Metric label="Tracked Orders" value={String(orders.length)} detail={`${filled} fills recorded`} icon={BookOpen} />
      <Metric label="Gateway Status" value={status === 'connected' ? 'Active' : 'Inactive'} detail="Railway High-Freq" icon={status === 'connected' ? Wifi : WifiOff} />
      <Metric label="System Events" value={String(activity.length)} detail="Audit Trail" icon={Activity} />
    </div>
    <div className="content-grid">
      <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
        <CardHeader>
          <p className="eyebrow">System Guard</p>
          <CardTitle style={{ fontSize: '18px', fontWeight: '600' }}>Risk Controls</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="empty-state" style={{ color: 'var(--text-muted)' }}>
            <CheckCircle2 className="h-8 w-8" style={{ color: 'var(--accent-green)' }} />
            <strong style={{ color: 'white' }}>Pre-trade validation active</strong>
            <span>All orders are scrubbed for risk before matching.</span>
          </div>
        </CardContent>
      </Card>
      <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
        <CardHeader>
          <p className="eyebrow">Real-time</p>
          <CardTitle style={{ fontSize: '18px', fontWeight: '600' }}>Latest Activity</CardTitle>
        </CardHeader>
        <CardContent>
          <ActivityList items={activity.slice(0, 5)} />
        </CardContent>
      </Card>
    </div>
  </div>;
}

function Metric({ label, value, detail, icon: Icon }: { label: string; value: string; detail: string; icon: typeof Activity }) {
  return <Card className="metric-card"><CardContent><div className="metric-icon"><Icon className="h-4 w-4" /></div><span>{label}</span><strong>{value}</strong><small>{detail}</small></CardContent></Card>;
}

function TradePanel({ order, setOrder, estimatedValue, livePrice, onSubmit, connected, notice }: { order: typeof defaultOrder; setOrder: Dispatch<SetStateAction<typeof defaultOrder>>; estimatedValue: number; livePrice: number; onSubmit: () => void; connected: boolean; notice: string }) {
  return <div className="page-stack">
    <div className="section-heading">
      <div>
        <p className="eyebrow">Order Entry</p>
        <h2 style={{ fontSize: '28px', fontWeight: '800', letterSpacing: '-0.04em' }}>Execution Ticket</h2>
        <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>High-frequency order routing via Railway gateway.</p>
      </div>
      <Badge variant={connected ? 'default' : 'secondary'} style={{ backgroundColor: connected ? 'rgba(0, 255, 148, 0.1)' : 'transparent', color: connected ? 'var(--accent-green)' : 'var(--text-muted)', border: '1px solid var(--border-color)' }}>
        {connected ? 'Gateway Ready' : 'Offline'}
      </Badge>
    </div>
    <div className="content-grid trade-grid">
      <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
        <CardHeader>
          <CardTitle style={{ fontSize: '18px', fontWeight: '600' }}>Order Parameters</CardTitle>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="side-toggle" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
            <Button variant={order.side === 'Buy' ? 'primary' : 'outline'} onClick={() => setOrder({ ...order, side: 'Buy' })} style={{ backgroundColor: order.side === 'Buy' ? 'var(--accent-cyan)' : 'transparent', color: 'var(--bg-deep)', fontWeight: '700' }}>Buy</Button>
            <Button variant={order.side === 'Sell' ? 'destructive' : 'outline'} onClick={() => setOrder({ ...order, side: 'Sell' })} style={{ backgroundColor: order.side === 'Sell' ? 'var(--accent-red)' : 'transparent', color: 'white', fontWeight: '700' }}>Sell</Button>
          </div>
          <label className="mono">SYMBOL<Input value={order.symbol} onChange={(event) => setOrder({ ...order, symbol: event.target.value })} className="mono" style={{ backgroundColor: 'var(--bg-deep)', color: 'white', border: '1px solid var(--border-color)' }} /></label>
          <div className="form-grid" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
            <label className="mono">QUANTITY<Input type="number" min="1" value={order.quantity} onChange={(event) => setOrder({ ...order, quantity: Number(event.target.value) })} className="mono" style={{ backgroundColor: 'var(--bg-deep)', color: 'white', border: '1px solid var(--border-color)' }} /></label>
            <label className="mono">TYPE<select value={order.type} onChange={(event) => setOrder({ ...order, type: event.target.value as OrderType })} style={{ backgroundColor: 'var(--bg-deep)', color: 'white', border: '1px solid var(--border-color)' }}><option>Limit</option><option>Market</option></select></label>
          </div>
          {order.type === 'Limit' && <label className="mono">LIMIT PRICE<Input type="number" min="0" value={order.price} onChange={(event) => setOrder({ ...order, price: Number(event.target.value) })} className="mono" style={{ backgroundColor: 'var(--bg-deep)', color: 'white', border: '1px solid var(--border-color)' }} /></label>}
          <label className="mono">TIF<select value={order.tif} onChange={(event) => setOrder({ ...order, tif: event.target.value as TimeInForce })} style={{ backgroundColor: 'var(--bg-deep)', color: 'white', border: '1px solid var(--border-color)' }}><option>GTC</option><option>IOC</option><option>FOK</option></select></label>
          <Button className="w-full" disabled={!connected || order.quantity <= 0 || !order.symbol} onClick={onSubmit} style={{ backgroundColor: 'var(--accent-cyan)', color: 'var(--bg-deep)', fontWeight: '800', fontSize: '14px', padding: '12px' }}>
            SUBMIT {order.side.toUpperCase()} ORDER
          </Button>
          {notice && <p className="success-text" style={{ color: 'var(--accent-green)', fontSize: '12px', textAlign: 'center', fontFamily: 'JetBrains Mono' }}>{notice}</p>}
        </CardContent>
      </Card>
      <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
        <CardHeader>
          <CardTitle style={{ fontSize: '18px', fontWeight: '600' }}>Market Snapshot</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="price-display" style={{ padding: '20px 0', textAlign: 'center' }}>
            <span className="mono" style={{ color: 'var(--text-muted)', fontSize: '12px' }}>{order.symbol.toUpperCase()} / USD</span>
            <strong className="mono" style={{ fontSize: '42px', color: 'white', display: 'block', margin: '8px 0' }}>{formatMoney(livePrice)}</strong>
            <small className="mono" style={{ color: 'var(--accent-green)', fontSize: '11px' }}><TrendingUp className="mr-1 inline h-3 w-3" /> Real-time simulated feed</small>
          </div>
          <div className="quote-row" style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--border-color)', padding: '16px 0', color: 'var(--text-muted)', fontSize: '12px', fontFamily: 'JetBrains Mono' }}>
            <span>Estimated Notional</span>
            <strong style={{ color: 'white' }}>{formatMoney(estimatedValue)}</strong>
          </div>
          <div className="quote-row" style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--border-color)', padding: '16px 0', color: 'var(--text-muted)', fontSize: '12px', fontFamily: 'JetBrains Mono' }}>
            <span>Execution Engine</span>
            <strong style={{ color: 'white' }}>Railway Matching</strong>
          </div>
          <div className="quote-row" style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--border-color)', padding: '16px 0', color: 'var(--text-muted)', fontSize: '12px', fontFamily: 'JetBrains Mono' }}>
            <span>Identity Token</span>
            <strong style={{ color: 'white' }}>Firebase Auth</strong>
          </div>
        </CardContent>
      </Card>
    </div>
  </div>;
}

function OrdersPanel({ orders }: { orders: OrderResponse[] }) {
  return <div className="page-stack">
    <div className="section-heading">
      <div>
        <p className="eyebrow">Order Management</p>
        <h2 style={{ fontSize: '28px', fontWeight: '800', letterSpacing: '-0.04em' }}>Execution Log</h2>
      </div>
      <Badge variant="secondary" style={{ backgroundColor: 'var(--bg-surface)', color: 'var(--text-muted)', border: '1px solid var(--border-color)', fontFamily: 'JetBrains Mono' }}>
        {orders.length} Events
      </Badge>
    </div>
    <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
      <CardContent>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Order ID</th>
                <th>Status</th>
                <th>Quantity</th>
                <th>Price</th>
                <th>Message</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <tr key={order.OrderId}>
                  <td className="mono" style={{ color: 'var(--accent-cyan)' }}>{order.OrderId.slice(0, 12)}…</td>
                  <td>
                    <Badge variant={order.Status === 'Rejected' ? 'destructive' : 'default'} style={{
                      backgroundColor: order.Status === 'Rejected' ? 'var(--accent-red)' : 'var(--accent-green)',
                      color: 'var(--bg-deep)',
                      fontWeight: '700',
                      fontSize: '10px'
                    }}>
                      {order.Status}
                    </Badge>
                  </td>
                  <td className="mono">{order.ExecutedQuantity}</td>
                  <td className="mono">{order.ExecutedPrice ? formatMoney(order.ExecutedPrice) : '—'}</td>
                  <td style={{ color: 'var(--text-muted)', fontSize: '11px' }}>{order.Message}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {orders.length === 0 && (
            <div className="empty-state" style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
              <BookOpen className="h-8 w-8 mb-2" />
              <strong style={{ color: 'white', display: 'block' }}>No execution history</strong>
              <span>Submit an order from the Trade ticket to populate this log.</span>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  </div>;
}

function PortfolioPanel({ livePrice, setLivePrice, orders }: { livePrice: number; setLivePrice: (value: number) => void; orders: OrderResponse[] }) {
  return <div className="page-stack">
    <div className="section-heading">
      <div>
        <p className="eyebrow">Account View</p>
        <h2 style={{ fontSize: '28px', fontWeight: '800', letterSpacing: '-0.04em' }}>Portfolio</h2>
        <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>Consolidated simulated asset holdings and market marks.</p>
      </div>
    </div>
    <div className="metric-grid">
      <Metric label="Cash Balance" value="$100,000.00" detail="Simulated Starting Capital" icon={CircleDollarSign} />
      <Metric label="AAPL Mark" value={formatMoney(livePrice)} detail="Market Data Feed" icon={TrendingUp} />
      <Metric label="Filled Orders" value={String(orders.filter((item) => item.Status !== 'Rejected').length)} detail="Session Executions" icon={BarChart3} />
    </div>
    <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
      <CardHeader>
        <p className="eyebrow">Feed Control</p>
        <CardTitle style={{ fontSize: '18px', fontWeight: '600' }}>Market Data Simulation</CardTitle>
      </CardHeader>
      <CardContent>
        <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '20px' }}>
          Adjust the simulated mark price to test UI responsiveness and trade triggers.
        </p>
        <div className="form-grid" style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: '16px', alignItems: 'end' }}>
          <label className="mono">AAPL PRICE<Input type="number" value={livePrice} onChange={(event) => setLivePrice(Number(event.target.value))} className="mono" style={{ backgroundColor: 'var(--bg-deep)', color: 'white', border: '1px solid var(--border-color)' }} /></label>
          <div className="feed-status" style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--accent-green)', fontSize: '12px', fontFamily: 'JetBrains Mono' }}>
            <span className="status-dot status-dot-live" />
            FEED ACTIVE
          </div>
        </div>
      </CardContent>
    </Card>
  </div>;
}

function ActivityPanel({ activity, onClear }: { activity: ActivityItem[]; onClear: () => void }) {
  return <div className="page-stack">
    <div className="section-heading">
      <div>
        <p className="eyebrow">Audit Trail</p>
        <h2 style={{ fontSize: '28px', fontWeight: '800', letterSpacing: '-0.04em' }}>System Activity</h2>
        <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>Immutable event log synchronized via Firestore.</p>
      </div>
      <Button variant="outline" onClick={onClear} style={{ borderColor: 'var(--border-color)', color: 'var(--text-muted)', fontSize: '11px' }}>Purge View</Button>
    </div>
    <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
      <CardContent>
        <ActivityList items={activity} />
      </CardContent>
    </Card>
  </div>;
}

function ActivityList({ items }: { items: ActivityItem[] }) {
  if (!items.length) return <div className="empty-state" style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}><Clock3 className="h-8 w-8 mb-2" /><span>No activity recorded in current session.</span></div>;
  return <div className="activity-list">{items.map((item) => <div className="activity-row" key={item.id}><span className={cn('activity-icon', item.type === 'error' && 'activity-error', item.type === 'success' && 'activity-success')}><Activity className="h-4 w-4" /></span><div><strong style={{ color: 'white', fontSize: '13px' }}>{item.message}</strong><small className="mono" style={{ color: 'var(--text-muted)', marginTop: '4px', display: 'block' }}>{item.createdAt?.toDate?.()?.toLocaleString() ?? 'Just now'}</small></div></div>)}</div>;
}

function SettingsPanel({ user, status, onConnect, onDisconnect }: { user: User; status: ConnectionStatus; onConnect: () => void; onDisconnect: () => void }) {
  return <div className="page-stack">
    <div className="section-heading">
      <div>
        <p className="eyebrow">Configuration</p>
        <h2 style={{ fontSize: '28px', fontWeight: '800', letterSpacing: '-0.04em' }}>Workspace Settings</h2>
      </div>
    </div>
    <div className="content-grid">
      <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
        <CardHeader>
          <CardTitle style={{ fontSize: '18px', fontWeight: '600' }}>Identity</CardTitle>
        </CardHeader>
        <CardContent className="settings-list" style={{ display: 'grid', gap: '20px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', color: 'var(--accent-cyan)' }}>
            <UserRound className="h-5 w-5" />
            <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Registered Email <strong style={{ color: 'white', marginLeft: '8px', fontFamily: 'JetBrains Mono' }}>{user.email}</strong></span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', color: 'var(--accent-cyan)' }}>
            <ShieldCheck className="h-5 w-5" />
            <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Auth Provider <strong style={{ color: 'white', marginLeft: '8px', fontFamily: 'JetBrains Mono' }}>Firebase Identity</strong></span>
          </div>
        </CardContent>
      </Card>
      <Card style={{ backgroundColor: 'var(--bg-surface)', border: '1px solid var(--border-color)' }}>
        <CardHeader>
          <CardTitle style={{ fontSize: '18px', fontWeight: '600' }}>Infrastructure</CardTitle>
        </CardHeader>
        <CardContent className="settings-list" style={{ display: 'grid', gap: '20px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', color: 'var(--accent-cyan)' }}>
            <Wifi className="h-5 w-5" />
            <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Gateway Endpoint <strong style={{ color: 'white', marginLeft: '8px', fontFamily: 'JetBrains Mono', fontSize: '11px' }}>{import.meta.env.VITE_WS_URL ?? 'Railway Production'}</strong></span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', color: 'var(--accent-cyan)' }}>
            <span className={cn('status-dot', status === 'connected' && 'status-dot-live')} />
            <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Connection State <strong style={{ color: 'white', marginLeft: '8px', fontFamily: 'JetBrains Mono' }}>{status}</strong></span>
          </div>
          <div style={{ marginTop: '10px' }}>
            {status === 'connected' ? <Button variant="outline" onClick={onDisconnect} style={{ borderColor: 'var(--border-color)', color: 'var(--text-muted)' }}>Disconnect Gateway</Button> : <Button onClick={onConnect} style={{ backgroundColor: 'var(--accent-cyan)', color: 'var(--bg-deep)', fontWeight: '700' }}>Establish Connection</Button>}
          </div>
        </CardContent>
      </Card>
    </div>
  </div>;
}
