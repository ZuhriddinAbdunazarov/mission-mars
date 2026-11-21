// Small JPG copies of the images the home page uses (the originals stay in Book/img for the book).
//   node optimize-images.mjs <Book/img dir> <Site/img dir>
import Jimp from 'jimp';
import fs from 'fs';
import path from 'path';

const [, , src, dst] = process.argv;
fs.mkdirSync(dst, { recursive: true });
const jobs = [
  ['PIA24542.jpg', 'hero.jpg', 1920],
  ['PIA25681.jpg', 'mars-rover.jpg', 1600],
  ...['wake', 'storm', 'bunker', 'puzzle', 'meteor', 'earth', 'launch', 'hologram', 'title'].map((n) => [`game_${n}.png`, `game_${n}.jpg`, 1100]),
];
for (const [inName, outName, width] of jobs) {
  const img = await Jimp.read(path.join(src, inName));
  if (img.bitmap.width > width) img.resize(width, Jimp.AUTO);
  await img.quality(78).writeAsync(path.join(dst, outName));
  console.log(outName, Math.round(fs.statSync(path.join(dst, outName)).size / 1024) + ' KB');
}
