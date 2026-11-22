// Call NASA ONCE (at build time) and save the real numbers + pictures inside the game, so it starts
// instantly and still has real NASA data when the internet or an API is down.
//   node Site/tools/nasa-snapshot.mjs
// Key: NASA_API_KEY from the environment, or from ../research/.env (never committed).
// Writes MissionMars/Assets/_Project/Resources/Data/nasa_snapshot.json and Resources/Tex/NASA_*.jpg.
// Anything that fails keeps the value from the previous snapshot.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const RES = path.join(ROOT, 'MissionMars/Assets/_Project/Resources');
const OUT = path.join(RES, 'Data/nasa_snapshot.json');
const TEX = path.join(RES, 'Tex');

function key() {
  if (process.env.NASA_API_KEY) return process.env.NASA_API_KEY;
  const env = path.join(ROOT, '../research/.env');
  if (fs.existsSync(env)) { const m = /^NASA_API_KEY=(.+)$/m.exec(fs.readFileSync(env, 'utf8')); if (m) return m[1].trim(); }
  return 'DEMO_KEY';
}
const KEY = key();
const day = (d) => d.toISOString().slice(0, 10);
const ago = (n) => new Date(Date.now() - n * 86400000);

async function get(url, { json = true, timeout = 30000 } = {}) {
  for (let i = 0; i < 2; i++) {
    try {
      const r = await fetch(url, { signal: AbortSignal.timeout(timeout) });
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      return json ? await r.json() : Buffer.from(await r.arrayBuffer());
    } catch (e) { if (i === 1) throw e; await new Promise((s) => setTimeout(s, 1500)); }
  }
}
const safe = (s) => s.replace(/api_key=[^&]+/, 'api_key=…');

const old = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : {};
const snap = { ...old, retrievedAt: new Date().toISOString(), sources: { ...(old.sources || {}) } };
const log = (name, ok, extra = '') => console.log(`${ok ? '✔' : '✖'} ${name}${extra ? ': ' + extra : ''}`);

// 1. Solar flares (NASA DONKI): last 30 days; if NASA's FLR endpoint is down keep the previous list
for (const url of [
  `https://api.nasa.gov/DONKI/FLR?startDate=${day(ago(30))}&endDate=${day(new Date())}&api_key=${KEY}`,
  `https://kauai.ccmc.gsfc.nasa.gov/DONKI/WS/get/FLR?startDate=${day(ago(30))}&endDate=${day(new Date())}`,
]) {
  try {
    const items = await get(url);
    if (!Array.isArray(items)) throw new Error('not a list');
    snap.flares = items.filter((f) => f.classType).map((f) => ({ classType: f.classType, peakTime: f.peakTime, beginTime: f.beginTime, sourceLocation: f.sourceLocation || '' }));
    snap.sources.flares = { from: safe(url).split('?')[0], at: snap.retrievedAt };
    log('DONKI flares', true, `${snap.flares.length}`); break;
  } catch (e) { log('DONKI flares', false, `${safe(url).split('?')[0]} ${e.message}`); }
}

// 2. Solar plasma clouds (NASA DONKI CME)
try {
  const items = await get(`https://api.nasa.gov/DONKI/CME?startDate=${day(ago(30))}&endDate=${day(new Date())}&api_key=${KEY}`);
  snap.cmeCount = items.length; snap.cmeLatest = items.length ? items[items.length - 1].startTime : '';
  snap.sources.cme = { from: 'api.nasa.gov DONKI CME', at: snap.retrievedAt };
  log('DONKI CMEs', true, `${items.length}`);
} catch (e) { log('DONKI CMEs', false, e.message); }

// 3. Mars weather (NASA InSight)
try {
  const j = await get(`https://api.nasa.gov/insight_weather/?feedtype=json&ver=1.0&api_key=${KEY}`);
  snap.sols = (j.sol_keys || []).map((k) => {
    const s = j[k];
    return { sol: +k, date: (s.First_UTC || '').slice(0, 10), season: s.Season || '', tempMinC: s.AT?.mn, tempAvgC: s.AT?.av, tempMaxC: s.AT?.mx, pressurePa: s.PRE?.av, windMs: s.HWS?.av, windDir: s.WD?.most_common?.compass_point || '' };
  });
  snap.sources.insight = { from: 'api.nasa.gov InSight', at: snap.retrievedAt };
  log('InSight weather', true, `${snap.sols.length} sols`);
} catch (e) { log('InSight weather', false, e.message); }

// 4. Astronomy Picture of the Day (text + picture)
try {
  const a = await get(`https://api.nasa.gov/planetary/apod?thumbs=true&api_key=${KEY}`, { timeout: 45000 });
  const img = a.media_type === 'image' ? a.url : a.thumbnail_url;
  snap.apod = { title: a.title, date: a.date, explanation: a.explanation, url: a.url, media_type: a.media_type, copyright: (a.copyright || '').trim() };
  if (img) { fs.writeFileSync(path.join(TEX, 'NASA_apod.jpg'), await get(img, { json: false })); snap.apod.image = 'NASA_apod'; }
  snap.sources.apod = { from: 'api.nasa.gov APOD', at: snap.retrievedAt };
  log('APOD', true, a.title);
} catch (e) { log('APOD', false, e.message); }

// 5. Three real Mars photos (NASA Image and Video Library)
try {
  const j = await get('https://images-api.nasa.gov/search?q=' + encodeURIComponent('perseverance rover mars surface') + '&media_type=image&page_size=20');
  const photos = [];
  for (const it of j.collection.items) {
    if (photos.length >= 3) break;
    const href = (it.links || []).find((l) => /~thumb\.jpg$/.test(l.href))?.href; if (!href) continue;
    try {
      const n = photos.length;
      fs.writeFileSync(path.join(TEX, `NASA_photo${n}.jpg`), await get(href, { json: false }));
      photos.push({ title: it.data[0].title, nasaId: it.data[0].nasa_id, image: `NASA_photo${n}` });
    } catch { /* try the next one */ }
  }
  if (photos.length) { snap.photos = photos; snap.sources.photos = { from: 'images-api.nasa.gov', at: snap.retrievedAt }; }
  log('NASA photos', photos.length > 0, `${photos.length}`);
} catch (e) { log('NASA photos', false, e.message); }

// 6. Earth–Mars distance right now (NASA/JPL Horizons, no key)
try {
  const t = new Date().toISOString().slice(0, 16).replace('T', ' ');
  const q = (id) => `https://ssd.jpl.nasa.gov/api/horizons.api?format=json&COMMAND='${id}'&OBJ_DATA='NO'&EPHEM_TYPE='VECTORS'&CENTER='500@10'&START_TIME='${t}'&STOP_TIME='${t}:01'&STEP_SIZE='1'&VEC_TABLE='1'&CSV_FORMAT='YES'&REF_PLANE='ECLIPTIC'`;
  const vec = async (id) => { const r = (await get(q(id))).result; const row = r.slice(r.indexOf('$$SOE') + 5, r.indexOf('$$EOE')).trim().split('\n')[0].split(','); return [+row[2], +row[3], +row[4]]; };
  const [e, m] = [await vec('399'), await vec('499')];
  snap.earthMarsKm = Math.round(Math.hypot(m[0] - e[0], m[1] - e[1], m[2] - e[2]));
  snap.sources.horizons = { from: 'ssd.jpl.nasa.gov Horizons', at: snap.retrievedAt };
  log('JPL Horizons', true, `${(snap.earthMarsKm / 1e6).toFixed(1)} million km`);
} catch (e) { log('JPL Horizons', false, e.message); }

fs.writeFileSync(OUT, JSON.stringify(snap, null, 1));
console.log('saved', path.relative(ROOT, OUT));
