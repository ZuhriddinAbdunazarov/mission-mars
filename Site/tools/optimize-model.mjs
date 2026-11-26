// Makes a lighter copy of a .glb or .gltf for the web build.
//   node optimize-model.mjs <in.glb|gltf> <out.glb|gltf> <maxTexturePx> [meshRatio] [maxError]
// - textures larger than maxTexturePx are scaled down (Unity keeps glb textures uncompressed,
//   so resolution is what decides the download size)
// - meshRatio < 1 simplifies the mesh to about that share of its triangles (meshoptimizer)
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { weld, simplify } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import Jimp from 'jimp';
import JPEG from 'jpeg-js';

Jimp.decoders['image/jpeg'] = (d) => JPEG.decode(d, { maxMemoryUsageInMB: 4096, maxResolutionInMP: 200 });

const [, , input, output, maxStr, ratioStr, errStr] = process.argv; // errStr: allowed shape error (default 0.002, bigger = fewer triangles)
const max = Number(maxStr);
const ratio = ratioStr ? Number(ratioStr) : 1;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS); // keep emissive strength etc.
const doc = await io.read(input);

for (const tex of max < 99999 ? doc.getRoot().listTextures() : []) { // 99999 = keep the pictures as they are
  const mime = tex.getMimeType();
  const img = tex.getImage();
  if (!img || !/jpeg|png/.test(mime)) continue;
  const j = await Jimp.read(Buffer.from(img));
  const w = j.bitmap.width, h = j.bitmap.height;
  if (Math.max(w, h) <= max) continue;
  const s = max / Math.max(w, h);
  j.resize(Math.max(1, Math.round(w * s)), Math.max(1, Math.round(h * s)));
  const buf = mime.includes('jpeg') ? await j.quality(88).getBufferAsync(Jimp.MIME_JPEG) : await j.getBufferAsync(Jimp.MIME_PNG);
  tex.setImage(new Uint8Array(buf));
}

if (ratio < 1) {
  await MeshoptSimplifier.ready;
  await doc.transform(weld(), simplify({ simplifier: MeshoptSimplifier, ratio, error: errStr ? Number(errStr) : 0.002 }));
}

await io.write(output, doc);
console.log(`ok ${input.split(/[\\/]/).pop()} tex<=${max}${ratio < 1 ? ` mesh x${ratio}` : ''}`);
