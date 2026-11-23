import { NodeIO } from '@gltf-transform/core';
import fs from 'node:fs';
const [, , input, out, NXs, NZs] = process.argv; const NX = +NXs, NZ = +NZs;
const doc = await new NodeIO().read(input);
const pos = doc.getRoot().listMeshes()[0].listPrimitives()[0].getAttribute('POSITION');
let minx = 1e9, maxx = -1e9, minz = 1e9, maxz = -1e9;
for (let i = 0; i < pos.getCount(); i++) { const [x, , z] = pos.getElement(i, []); minx = Math.min(minx, x); maxx = Math.max(maxx, x); minz = Math.min(minz, z); maxz = Math.max(maxz, z); }
const g = new Float32Array(NX * NZ);
for (let i = 0; i < pos.getCount(); i++) { const [x, y, z] = pos.getElement(i, []); const gx = Math.min(NX - 1, Math.floor((x - minx) / (maxx - minx) * NX)), gz = Math.min(NZ - 1, Math.floor((z - minz) / (maxz - minz) * NZ)); if (y > g[gz * NX + gx]) g[gz * NX + gx] = y; }
let s = `# max height per cell (model units); line 2: NX NZ minx maxx minz maxz; then NZ rows of NX values (x across, z down)\n${NX} ${NZ} ${minx.toFixed(4)} ${maxx.toFixed(4)} ${minz.toFixed(4)} ${maxz.toFixed(4)}\n`;
for (let z = 0; z < NZ; z++) s += Array.from(g.slice(z * NX, z * NX + NX), (v) => v.toFixed(3)).join(' ') + '\n';
fs.writeFileSync(out, s); console.log('heights', NX, 'x', NZ);
