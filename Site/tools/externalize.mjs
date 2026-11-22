// Turn a .glb into .gltf + .bin + separate image files, so Unity imports the pictures as normal textures
// and COMPRESSES them (inside a .glb, glTFast keeps them uncompressed: 4-10x bigger downloads).
//   node externalize.mjs input.glb outDir name
// Image files are named <name>_<i>_<role>.<ext>, role = color | normal | mr | occlusion | emissive,
// so ProjectSetup.TexImport knows which ones are linear data (normal, mr, occlusion).
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import fs from 'node:fs';
import path from 'node:path';

const [, , input, outDir, name] = process.argv;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(input);
const role = new Map();
for (const m of doc.getRoot().listMaterials()) {
  const set = (t, r) => { if (t && !role.has(t)) role.set(t, r); };
  set(m.getBaseColorTexture(), 'color'); set(m.getNormalTexture(), 'normal'); set(m.getMetallicRoughnessTexture(), 'mr');
  set(m.getOcclusionTexture(), 'occlusion'); set(m.getEmissiveTexture(), 'emissive');
}
doc.getRoot().listTextures().forEach((t, i) => {
  const ext = t.getMimeType() === 'image/png' ? 'png' : 'jpg';
  t.setURI(`${name}_${i}_${role.get(t) || 'color'}.${ext}`);
});
doc.getRoot().listBuffers().forEach((b) => b.setURI(`${name}.bin`));
fs.mkdirSync(outDir, { recursive: true });
await io.write(path.join(outDir, `${name}.gltf`), doc);
console.log('externalized', name, doc.getRoot().listTextures().length, 'textures');
