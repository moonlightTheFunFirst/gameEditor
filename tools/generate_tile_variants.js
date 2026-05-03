const fs = require('fs');
const path = require('path');

const W = 256;
const H = 256;
const T = 32;
const MAG = [255, 0, 255];

const clamp = (v) => Math.max(0, Math.min(255, v | 0));
const shade = (c, d) => [clamp(c[0] + d), clamp(c[1] + d), clamp(c[2] + d)];

function rnd(seed) {
  let s = seed >>> 0;
  return () => ((s = (Math.imul(1664525, s) + 1013904223) >>> 0) / 4294967296);
}

function img(bg) {
  const a = new Uint8Array(W * H * 3);
  for (let i = 0; i < a.length; i += 3) {
    a[i] = bg[0];
    a[i + 1] = bg[1];
    a[i + 2] = bg[2];
  }
  return a;
}

function px(a, x, y, c) {
  x |= 0;
  y |= 0;
  if (x < 0 || y < 0 || x >= W || y >= H) {
    return;
  }
  const i = (y * W + x) * 3;
  a[i] = c[0];
  a[i + 1] = c[1];
  a[i + 2] = c[2];
}

function rect(a, x, y, w, h, c) {
  for (let yy = Math.max(0, y | 0); yy < Math.min(H, (y + h) | 0); yy++) {
    for (let xx = Math.max(0, x | 0); xx < Math.min(W, (x + w) | 0); xx++) {
      px(a, xx, yy, c);
    }
  }
}

function line(a, x0, y0, x1, y1, c, th = 1) {
  x0 |= 0;
  y0 |= 0;
  x1 |= 0;
  y1 |= 0;
  let dx = Math.abs(x1 - x0);
  let sx = x0 < x1 ? 1 : -1;
  let dy = -Math.abs(y1 - y0);
  let sy = y0 < y1 ? 1 : -1;
  let err = dx + dy;
  for (;;) {
    rect(a, x0 - (th >> 1), y0 - (th >> 1), th, th, c);
    if (x0 === x1 && y0 === y1) {
      break;
    }
    const e2 = 2 * err;
    if (e2 >= dy) {
      err += dy;
      x0 += sx;
    }
    if (e2 <= dx) {
      err += dx;
      y0 += sy;
    }
  }
}

function ellipse(a, cx, cy, rx, ry, c) {
  for (let y = -ry; y <= ry; y++) {
    for (let x = -rx; x <= rx; x++) {
      if ((x * x) / (rx * rx) + (y * y) / (ry * ry) <= 1) {
        px(a, cx + x, cy + y, c);
      }
    }
  }
}

function tri(a, p1, p2, p3, c) {
  const minX = Math.max(0, Math.min(p1[0], p2[0], p3[0]) | 0);
  const maxX = Math.min(W - 1, Math.max(p1[0], p2[0], p3[0]) | 0);
  const minY = Math.max(0, Math.min(p1[1], p2[1], p3[1]) | 0);
  const maxY = Math.min(H - 1, Math.max(p1[1], p2[1], p3[1]) | 0);
  const side = (p, q, r) => (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]);

  for (let y = minY; y <= maxY; y++) {
    for (let x = minX; x <= maxX; x++) {
      const p = [x, y];
      const a1 = side(p1, p2, p);
      const a2 = side(p2, p3, p);
      const a3 = side(p3, p1, p);
      if ((a1 >= 0 && a2 >= 0 && a3 >= 0) || (a1 <= 0 && a2 <= 0 && a3 <= 0)) {
        px(a, x, y, c);
      }
    }
  }
}

function border(a, x, y, w, h, c) {
  line(a, x, y, x + w - 1, y, c);
  line(a, x, y + h - 1, x + w - 1, y + h - 1, c);
  line(a, x, y, x, y + h - 1, c);
  line(a, x + w - 1, y, x + w - 1, y + h - 1, c);
}

const tx = (i) => (i % 8) * T;
const ty = (i) => Math.floor(i / 8) * T;

function fillTile(a, i, c) {
  rect(a, tx(i), ty(i), T, T, c);
}

function speck(a, i, c1, c2, n, seed = 1, sz = 2) {
  const r = rnd((i + 1) * 7919 + seed * 131);
  const x0 = tx(i);
  const y0 = ty(i);
  for (let k = 0; k < n; k++) {
    const c = k % 2 ? c1 : c2;
    const s = 1 + Math.floor(r() * sz);
    rect(a, x0 + Math.floor(r() * 32), y0 + Math.floor(r() * 32), s, s, c);
  }
}

function grass(a, i, base = [74, 161, 48], dark = [43, 119, 41], light = [117, 190, 61], flowers = false) {
  fillTile(a, i, base);
  const r = rnd((i + 7) * 341 + 3);
  const x0 = tx(i);
  const y0 = ty(i);

  for (let k = 0; k < 76; k++) {
    const x = x0 + Math.floor(r() * 32);
    const y = y0 + Math.floor(r() * 32);
    rect(a, x, y, 1, 1 + Math.floor(r() * 3), k % 3 ? dark : light);
  }

  if (flowers) {
    const cs = [[246, 231, 95], [245, 246, 246], [239, 112, 145]];
    for (let k = 0; k < 9; k++) {
      const x = x0 + 3 + Math.floor(r() * 26);
      const y = y0 + 4 + Math.floor(r() * 24);
      rect(a, x, y, 2, 2, cs[k % 3]);
      rect(a, x, y + 2, 1, 2, dark);
    }
  }
}

function dirt(a, i, base = [147, 99, 49]) {
  fillTile(a, i, base);
  speck(a, i, shade(base, -25), shade(base, 24), 70, 11);
}

function sand(a, i) {
  fillTile(a, i, [218, 193, 116]);
  speck(a, i, [185, 156, 86], [239, 220, 151], 65, 12);
}

function water(a, i, base = [30, 116, 181], wave = [95, 177, 231]) {
  fillTile(a, i, base);
  const r = rnd((i + 5) * 1021);
  const x0 = tx(i);
  const y0 = ty(i);

  for (let y = 4; y < 32; y += 6) {
    for (let k = 0; k < 3; k++) {
      const x = x0 + Math.floor(r() * 20);
      line(a, x, y0 + y + Math.floor(r() * 3) - 1, x + 5 + Math.floor(r() * 8), y0 + y + Math.floor(r() * 3) - 1, wave);
    }
  }
  line(a, x0, y0 + 31, x0 + 31, y0 + 31, shade(base, -35));
}

function stone(a, i, base = [109, 112, 109], grout = [72, 76, 75]) {
  fillTile(a, i, base);
  const x0 = tx(i);
  const y0 = ty(i);
  for (let y = 0; y <= 32; y += 8) {
    line(a, x0, y0 + y, x0 + 31, y0 + y, grout);
  }
  for (let x = 0; x <= 32; x += 8) {
    line(a, x0 + x, y0, x0 + x, y0 + 31, grout);
  }
  speck(a, i, shade(base, -18), shade(base, 18), 35, 14);
}

function brick(a, i, base = [150, 71, 52], grout = [89, 48, 41]) {
  fillTile(a, i, base);
  const x0 = tx(i);
  const y0 = ty(i);
  for (let y = 0; y <= 32; y += 8) {
    line(a, x0, y0 + y, x0 + 31, y0 + y, grout);
  }
  for (let row = 0; row < 4; row++) {
    const off = (row % 2) * 8;
    for (let x = -off; x < 32; x += 16) {
      line(a, x0 + x, y0 + row * 8, x0 + x, y0 + row * 8 + 7, grout);
    }
  }
  speck(a, i, shade(base, -18), shade(base, 16), 28, 15);
}

function wood(a, i, base = [144, 93, 46]) {
  fillTile(a, i, base);
  const x0 = tx(i);
  const y0 = ty(i);
  for (let x = 0; x < 32; x += 8) {
    line(a, x0 + x, y0, x0 + x, y0 + 31, [88, 55, 27]);
    line(a, x0 + x + 1, y0, x0 + x + 1, y0 + 31, shade(base, 35));
  }
  const r = rnd(i * 991 + 16);
  for (let k = 0; k < 5; k++) {
    ellipse(a, x0 + 4 + Math.floor(r() * 24), y0 + 5 + Math.floor(r() * 22), 3, 2, [111, 69, 32]);
  }
}

function roof(a, i, base = [174, 54, 45], hi = [219, 93, 67]) {
  fillTile(a, i, base);
  const x0 = tx(i);
  const y0 = ty(i);
  for (let y = 3; y < 32; y += 6) {
    line(a, x0, y0 + y, x0 + 31, y0 + y, hi, 2);
  }
  for (let x = -20; x < 42; x += 8) {
    line(a, x0 + x, y0 + 31, x0 + x + 25, y0, shade(base, -35));
  }
}

function farm(a, i, d = [123, 80, 42], crop = [83, 160, 48]) {
  fillTile(a, i, d);
  const x0 = tx(i);
  const y0 = ty(i);
  for (let x = 2; x < 32; x += 6) {
    rect(a, x0 + x - 1, y0, 2, 32, shade(d, -25));
    for (let y = 3; y < 32; y += 7) {
      rect(a, x0 + x, y0 + y, 3, 4, crop);
    }
  }
}

function carpet(a, i, b = [132, 35, 43], bo = [224, 190, 80]) {
  fillTile(a, i, [61, 49, 54]);
  const x0 = tx(i);
  const y0 = ty(i);
  rect(a, x0 + 3, y0 + 3, 26, 26, b);
  border(a, x0 + 3, y0 + 3, 26, 26, bo);
  rect(a, x0 + 8, y0 + 8, 16, 16, shade(b, 30));
}

function checker(a, i, c1 = [216, 212, 190], c2 = [125, 120, 105]) {
  const x0 = tx(i);
  const y0 = ty(i);
  for (let y = 0; y < 4; y++) {
    for (let x = 0; x < 4; x++) {
      rect(a, x0 + x * 8, y0 + y * 8, 8, 8, (x + y) % 2 ? c2 : c1);
    }
  }
  border(a, x0, y0, 32, 32, [82, 82, 82]);
}

function cliff(a, i) {
  const x0 = tx(i);
  const y0 = ty(i);
  fillTile(a, i, [128, 102, 68]);
  rect(a, x0, y0, 32, 7, [70, 150, 50]);
  line(a, x0, y0 + 7, x0 + 31, y0 + 7, [34, 92, 35]);
  for (let k = 0; k < 5; k++) {
    const x = x0 + k * 7;
    tri(a, [x, y0 + 9], [x + 8, y0 + 9], [x + 2, y0 + 29], [92, 70, 47]);
    line(a, x + 2, y0 + 10, x + 7, y0 + 28, [176, 145, 92]);
  }
}

function pathTile(a, i, dir) {
  grass(a, i, [76, 149, 55], [50, 112, 40], [128, 184, 64]);
  const x0 = tx(i);
  const y0 = ty(i);
  const c = [157, 111, 60];
  if (dir.includes('v')) {
    rect(a, x0 + 11, y0, 10, 32, c);
  }
  if (dir.includes('h')) {
    rect(a, x0, y0 + 11, 32, 10, c);
  }
  speck(a, i, [121, 80, 47], [185, 140, 80], 35, 72);
}

function shore(a, i, side) {
  water(a, i, [37, 131, 189], [106, 193, 232]);
  const x0 = tx(i);
  const y0 = ty(i);
  const g = [74, 160, 51];
  if (side === 'n') rect(a, x0, y0, 32, 10, g);
  if (side === 's') rect(a, x0, y0 + 22, 32, 10, g);
  if (side === 'w') rect(a, x0, y0, 10, 32, g);
  if (side === 'e') rect(a, x0 + 22, y0, 10, 32, g);
  if (side === 'ne') {
    rect(a, x0, y0, 32, 10, g);
    rect(a, x0 + 22, y0, 10, 32, g);
  }
  if (side === 'nw') {
    rect(a, x0, y0, 32, 10, g);
    rect(a, x0, y0, 10, 32, g);
  }
  if (side === 'se') {
    rect(a, x0, y0 + 22, 32, 10, g);
    rect(a, x0 + 22, y0, 10, 32, g);
  }
  if (side === 'sw') {
    rect(a, x0, y0 + 22, 32, 10, g);
    rect(a, x0, y0, 10, 32, g);
  }
}

function baseDraw(a, i, d) {
  const [t, ...p] = d;
  if (t === 'grass') grass(a, i, ...p);
  else if (t === 'dirt') dirt(a, i, ...p);
  else if (t === 'sand') sand(a, i);
  else if (t === 'water') water(a, i, ...p);
  else if (t === 'stone') stone(a, i, ...p);
  else if (t === 'brick') brick(a, i, ...p);
  else if (t === 'wood') wood(a, i, ...p);
  else if (t === 'roof') roof(a, i, ...p);
  else if (t === 'farm') farm(a, i, ...p);
  else if (t === 'carpet') carpet(a, i, ...p);
  else if (t === 'checker') checker(a, i, ...p);
  else if (t === 'cliff') cliff(a, i);
  else if (t === 'path') pathTile(a, i, p[0]);
  else if (t === 'shore') shore(a, i, p[0]);
  else if (t === 'lava') {
    fillTile(a, i, [191, 58, 27]);
    speck(a, i, [105, 42, 34], [248, 115, 33], 58, 27);
  }
}

function saveBmp(file, a) {
  const row = W * 3;
  const size = 54 + row * H;
  const out = new Uint8Array(size);
  const v = new DataView(out.buffer);
  out[0] = 66;
  out[1] = 77;
  v.setUint32(2, size, true);
  v.setUint32(10, 54, true);
  v.setUint32(14, 40, true);
  v.setInt32(18, W, true);
  v.setInt32(22, H, true);
  v.setUint16(26, 1, true);
  v.setUint16(28, 24, true);
  v.setUint32(34, row * H, true);

  for (let y = 0; y < H; y++) {
    const sy = H - 1 - y;
    for (let x = 0; x < W; x++) {
      const si = (sy * W + x) * 3;
      const di = 54 + y * row + x * 3;
      out[di] = a[si + 2];
      out[di + 1] = a[si + 1];
      out[di + 2] = a[si];
    }
  }
  fs.writeFileSync(file, out);
}

const baseWorld = [
  ['grass', [74, 161, 48], [43, 119, 41], [117, 190, 61], false], ['grass', [102, 177, 58], [64, 132, 46], [150, 207, 83], true], ['grass', [49, 128, 53], [33, 89, 45], [89, 165, 63], false], ['grass', [157, 185, 77], [106, 137, 55], [194, 211, 93], true], ['dirt', [147, 99, 49]], ['sand'], ['dirt', [224, 229, 221]], ['grass', [75, 105, 72], [45, 78, 57], [101, 132, 86], false],
  ['water', [30, 116, 181], [95, 177, 231]], ['water', [15, 73, 139], [76, 141, 204]], ['water', [55, 153, 198], [136, 217, 239]], ['shore', 'n'], ['shore', 's'], ['shore', 'w'], ['shore', 'e'], ['shore', 'ne'],
  ['path', 'v'], ['path', 'h'], ['path', 'vh'], ['wood', [154, 105, 49]], ['stone', [93, 88, 78], [61, 58, 53]], ['dirt', [103, 74, 48]], ['water', [194, 232, 237], [242, 255, 255]], ['lava'],
  ['cliff'], ['dirt', [116, 92, 63]], ['stone', [99, 103, 100], [64, 68, 66]], ['dirt', [113, 92, 68]], ['dirt', [147, 83, 43]], ['grass', [171, 116, 48], [124, 78, 37], [208, 148, 58], false], ['farm', [109, 74, 39], [193, 177, 51]], ['farm', [123, 80, 42], [83, 160, 48]],
  ['farm', [106, 74, 38], [214, 179, 63]], ['grass', [90, 164, 52], [51, 117, 40], [140, 204, 77], true], ['stone', [143, 137, 122], [99, 96, 88]], ['wood', [189, 139, 75]], ['carpet', [150, 45, 43], [219, 181, 72]], ['stone', [128, 130, 132], [79, 82, 83]], ['brick', [150, 71, 52], [89, 48, 41]], ['stone', [88, 96, 103], [52, 57, 63]],
  ['brick', [117, 120, 125], [69, 72, 76]], ['roof', [174, 54, 45], [219, 93, 67]], ['roof', [60, 94, 160], [100, 143, 207]], ['roof', [143, 89, 40], [196, 128, 63]], ['roof', [64, 66, 80], [101, 104, 126]], ['wood', [127, 82, 41]], ['checker', [216, 212, 190], [125, 120, 105]], ['checker', [226, 221, 200], [83, 112, 150]],
  ['carpet', [91, 54, 147], [221, 186, 78]], ['carpet', [36, 126, 126], [224, 188, 89]], ['carpet', [124, 35, 50], [222, 188, 87]], ['stone', [72, 75, 73], [43, 45, 44]], ['brick', [68, 65, 60], [38, 37, 35]], ['grass', [93, 150, 48], [61, 105, 38], [135, 179, 66], false], ['grass', [43, 91, 63], [26, 63, 49], [76, 126, 85], false], ['dirt', [119, 51, 38]],
  ['stone', [171, 163, 142], [119, 113, 101]], ['grass', [76, 149, 55], [50, 112, 40], [128, 184, 64], true], ['shore', 'nw'], ['shore', 'se'], ['shore', 'sw'], ['stone', [156, 156, 154], [82, 85, 89]], ['water', [45, 132, 190], [124, 210, 234]], ['dirt', [30, 31, 34]]
];

const baseTown = [
  ['stone', [136, 132, 116], [92, 90, 82]], ['stone', [104, 106, 103], [70, 73, 71]], ['stone', [163, 156, 133], [112, 107, 93]], ['dirt', [152, 106, 62]], ['wood', [144, 93, 46]], ['wood', [183, 128, 65]], ['brick', [167, 82, 56], [101, 55, 44]], ['brick', [190, 154, 88], [127, 99, 61]],
  ['roof', [180, 55, 47], [228, 92, 69]], ['roof', [54, 103, 164], [91, 151, 215]], ['roof', [48, 130, 91], [85, 184, 133]], ['roof', [145, 88, 40], [204, 134, 59]], ['roof', [87, 86, 99], [129, 130, 147]], ['roof', [124, 43, 73], [181, 73, 111]], ['roof', [201, 166, 79], [240, 204, 99]], ['roof', [76, 55, 43], [122, 86, 62]],
  ['brick', [219, 186, 126], [151, 118, 75]], ['brick', [192, 174, 145], [123, 112, 95]], ['brick', [138, 111, 82], [88, 70, 55]], ['brick', [123, 128, 132], [72, 77, 81]], ['stone', [91, 95, 101], [55, 59, 64]], ['brick', [67, 68, 73], [38, 39, 43]], ['wood', [104, 66, 35]], ['checker', [236, 230, 203], [146, 137, 119]],
  ['stone', [116, 119, 124], [72, 75, 80]], ['brick', [96, 100, 108], [54, 58, 66]], ['stone', [73, 75, 80], [43, 44, 48]], ['carpet', [132, 35, 43], [224, 190, 80]], ['carpet', [40, 86, 151], [224, 190, 80]], ['carpet', [93, 46, 135], [224, 190, 80]], ['checker', [198, 196, 185], [96, 97, 102]], ['stone', [153, 154, 154], [100, 101, 103]],
  ['wood', [178, 124, 64]], ['wood', [112, 74, 39]], ['checker', [222, 210, 174], [129, 91, 65]], ['stone', [184, 176, 152], [127, 118, 101]], ['carpet', [38, 126, 126], [217, 183, 74]], ['carpet', [154, 76, 39], [234, 197, 89]], ['brick', [201, 161, 82], [140, 104, 58]], ['checker', [214, 207, 190], [57, 124, 151]],
  ['stone', [127, 121, 103], [80, 77, 69]], ['brick', [142, 119, 86], [85, 65, 50]], ['brick', [171, 76, 59], [108, 50, 43]], ['brick', [122, 70, 49], [78, 45, 35]], ['stone', [184, 185, 181], [126, 127, 125]], ['path', 'v'], ['path', 'h'], ['path', 'vh'],
  ['stone', [115, 118, 122], [78, 80, 86]], ['stone', [198, 187, 157], [145, 132, 105]], ['dirt', [139, 96, 55]], ['stone', [86, 88, 91], [55, 57, 60]], ['water', [45, 132, 190], [124, 210, 234]], ['stone', [156, 156, 154], [82, 85, 89]], ['stone', [71, 74, 78], [110, 112, 116]], ['stone', [42, 44, 48], [71, 73, 78]],
  ['grass', [83, 158, 55], [52, 115, 43], [127, 190, 68], false], ['stone', [156, 145, 116], [103, 95, 80]], ['brick', [190, 136, 74], [128, 88, 54]], ['stone', [112, 117, 121], [75, 80, 85]], ['checker', [216, 205, 185], [88, 129, 88]], ['carpet', [89, 122, 52], [219, 184, 77]], ['wood', [158, 97, 48]], ['dirt', [30, 31, 34]]
];

function tree(a, i, leaf = [48, 133, 50], leaf2 = [78, 169, 61]) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 14, y + 18, 5, 10, [96, 58, 29]);
  ellipse(a, x + 15, y + 16, 11, 9, [28, 72, 33]);
  ellipse(a, x + 11, y + 19, 9, 8, leaf);
  ellipse(a, x + 21, y + 15, 9, 9, leaf2);
  rect(a, x + 15, y + 18, 2, 10, [187, 137, 70]);
}

function pine(a, i, leaf = [44, 119, 54]) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 14, y + 21, 5, 8, [96, 58, 29]);
  tri(a, [x + 16, y + 2], [x + 5, y + 16], [x + 27, y + 16], [25, 68, 42]);
  tri(a, [x + 16, y + 8], [x + 3, y + 23], [x + 29, y + 23], leaf);
  tri(a, [x + 16, y + 14], [x + 6, y + 29], [x + 26, y + 29], shade(leaf, -10));
}

function mountain(a, i, rock = [156, 118, 69], snow = [232, 232, 220]) {
  const x = tx(i);
  const y = ty(i);
  tri(a, [x + 3, y + 28], [x + 16, y + 3], [x + 29, y + 28], [70, 61, 53]);
  tri(a, [x + 5, y + 27], [x + 16, y + 6], [x + 26, y + 27], rock);
  tri(a, [x + 16, y + 6], [x + 10, y + 17], [x + 16, y + 14], snow);
  tri(a, [x + 16, y + 6], [x + 16, y + 14], [x + 21, y + 18], snow);
  line(a, x + 16, y + 7, x + 16, y + 27, shade(rock, -45));
}

function rockObj(a, i, c = [129, 133, 131]) {
  const x = tx(i);
  const y = ty(i);
  tri(a, [x + 6, y + 25], [x + 12, y + 14], [x + 23, y + 13], [58, 59, 58]);
  tri(a, [x + 6, y + 25], [x + 23, y + 13], [x + 28, y + 25], [58, 59, 58]);
  tri(a, [x + 8, y + 24], [x + 14, y + 15], [x + 22, y + 15], c);
  tri(a, [x + 8, y + 24], [x + 22, y + 15], [x + 26, y + 24], c);
  line(a, x + 14, y + 16, x + 10, y + 23, shade(c, 55));
}

function house(a, i, roofC = [180, 59, 49], wall = [214, 181, 118]) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 5, y + 14, 22, 14, [73, 58, 45]);
  rect(a, x + 6, y + 15, 20, 13, wall);
  tri(a, [x + 3, y + 16], [x + 16, y + 5], [x + 29, y + 16], [88, 54, 42]);
  tri(a, [x + 5, y + 15], [x + 16, y + 7], [x + 27, y + 15], roofC);
  rect(a, x + 14, y + 20, 5, 8, [86, 53, 30]);
  rect(a, x + 8, y + 18, 4, 4, [102, 155, 197]);
  rect(a, x + 21, y + 18, 4, 4, [102, 155, 197]);
}

function crate(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 8, y + 14, 16, 12, [136, 95, 47]);
  border(a, x + 8, y + 14, 16, 12, [89, 58, 32]);
  line(a, x + 8, y + 14, x + 23, y + 25, [89, 58, 32]);
  line(a, x + 23, y + 14, x + 8, y + 25, [89, 58, 32]);
}

function barrel(a, i) {
  const x = tx(i);
  const y = ty(i);
  ellipse(a, x + 12, y + 20, 5, 7, [92, 61, 34]);
  ellipse(a, x + 21, y + 20, 5, 7, [92, 61, 34]);
  rect(a, x + 8, y + 16, 18, 8, [132, 86, 45]);
  line(a, x + 8, y + 17, x + 25, y + 17, [213, 165, 76]);
  line(a, x + 8, y + 23, x + 25, y + 23, [55, 42, 31]);
}

function sign(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 15, y + 16, 3, 11, [83, 54, 31]);
  rect(a, x + 7, y + 9, 19, 9, [204, 167, 84]);
  border(a, x + 7, y + 9, 19, 9, [93, 61, 34]);
}

function fence(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 4, y + 16, 24, 5, [106, 68, 37]);
  rect(a, x + 5, y + 11, 4, 13, [139, 87, 43]);
  rect(a, x + 23, y + 11, 4, 13, [139, 87, 43]);
  line(a, x + 4, y + 21, x + 28, y + 21, [70, 45, 29]);
}

function well(a, i) {
  const x = tx(i);
  const y = ty(i);
  ellipse(a, x + 16, y + 16, 10, 8, [64, 105, 116]);
  rect(a, x + 10, y + 15, 12, 7, [90, 66, 38]);
  rect(a, x + 8, y + 24, 16, 4, [151, 111, 56]);
}

function castle(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 6, y + 12, 20, 16, [112, 114, 116]);
  rect(a, x + 8, y + 7, 16, 8, [148, 151, 154]);
  rect(a, x + 14, y + 18, 5, 10, [53, 55, 58]);
  for (let k = 0; k < 3; k++) {
    rect(a, x + 8 + k * 6, y + 6, 4, 3, [90, 93, 99]);
  }
}

function banner(a, i, c = [127, 38, 48]) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 13, y + 6, 6, 21, c);
  tri(a, [x + 13, y + 7], [x + 25, y + 10], [x + 13, y + 14], [211, 170, 62]);
}

function crops(a, i, c = [76, 151, 43]) {
  const x = tx(i);
  const y = ty(i);
  for (let xx = 6; xx < 27; xx += 5) {
    for (let yy = 12; yy < 27; yy += 6) {
      rect(a, x + xx, y + yy, 3, 5, c);
    }
  }
}

function deadTree(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 13, y + 10, 6, 18, [93, 58, 31]);
  for (let k = 0; k < 4; k++) {
    line(a, x + 16, y + 15, x + 7 + k * 5, y + 7 + k * 3, [80, 44, 26], 2);
  }
}

function bridge(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 4, y + 13, 24, 6, [151, 98, 46]);
  rect(a, x + 4, y + 11, 24, 3, [201, 148, 71]);
  for (let xx = 6; xx < 28; xx += 5) {
    rect(a, x + xx, y + 8, 2, 15, [108, 69, 35]);
  }
}

function door(a, i, woodC = [126, 76, 39]) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 10, y + 9, 13, 19, [55, 38, 29]);
  rect(a, x + 11, y + 10, 11, 18, woodC);
  line(a, x + 16, y + 10, x + 16, y + 27, [96, 59, 34]);
  rect(a, x + 19, y + 19, 2, 2, [226, 185, 78]);
}

function windowObj(a, i, frame = [80, 55, 38], glass = [94, 164, 205]) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 7, y + 9, 18, 15, frame);
  rect(a, x + 9, y + 11, 14, 11, glass);
  line(a, x + 16, y + 11, x + 16, y + 22, frame, 2);
  line(a, x + 9, y + 16, x + 22, y + 16, frame, 2);
}

function awning(a, i, c1 = [187, 48, 50], c2 = [240, 198, 86]) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 5, y + 9, 22, 6, c1);
  rect(a, x + 5, y + 15, 22, 5, c2);
  for (let xx = 5; xx < 27; xx += 6) {
    rect(a, x + xx, y + 9, 3, 11, c2);
  }
  line(a, x + 5, y + 20, x + 27, y + 20, [92, 63, 37]);
}

function counter(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 5, y + 18, 22, 8, [119, 82, 43]);
  rect(a, x + 6, y + 14, 20, 5, [184, 131, 67]);
  line(a, x + 8, y + 20, x + 26, y + 20, [68, 45, 30]);
}

function shelf(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 7, y + 9, 18, 18, [132, 77, 42]);
  for (let yy = 11; yy < 25; yy += 5) {
    line(a, x + 9, y + yy, x + 23, y + yy, [224, 179, 86]);
  }
  for (let k = 0; k < 5; k++) {
    rect(a, x + 10 + k * 3, y + 12 + (k % 3) * 5, 2, 3, [70 + k * 30, 110 + k * 15, 70]);
  }
}

function bed(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 6, y + 11, 20, 14, [96, 60, 38]);
  rect(a, x + 8, y + 13, 16, 10, [220, 187, 133]);
  rect(a, x + 6, y + 10, 20, 3, [78, 45, 33]);
}

function table(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 5, y + 17, 22, 6, [135, 89, 48]);
  rect(a, x + 9, y + 12, 3, 8, [92, 58, 36]);
  rect(a, x + 21, y + 12, 3, 8, [92, 58, 36]);
  rect(a, x + 7, y + 15, 18, 3, [198, 151, 73]);
}

function chair(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 10, y + 11, 12, 15, [94, 61, 39]);
  rect(a, x + 12, y + 13, 8, 10, [201, 173, 114]);
  rect(a, x + 10, y + 10, 12, 3, [72, 46, 32]);
}

function throne(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 8, y + 5, 16, 23, [131, 35, 47]);
  rect(a, x + 9, y + 5, 14, 3, [220, 183, 69]);
  rect(a, x + 14, y + 8, 4, 15, [220, 183, 69]);
}

function stairs(a, i) {
  const x = tx(i);
  const y = ty(i);
  rect(a, x + 5, y + 20, 22, 7, [112, 115, 120]);
  rect(a, x + 7, y + 15, 18, 5, [71, 74, 79]);
  for (let k = 0; k < 4; k++) {
    rect(a, x + 8 + k * 4, y + 11 + k, 3, 4, [167, 169, 173]);
  }
}

const worldObjs = [
  'blank', 'tree', 'treeDark', 'pine', 'pineBlue', 'bush', 'flowers', 'deadTree',
  'log', 'mountain', 'snowMountain', 'darkMountain', 'rock', 'brownRock', 'hill', 'swamp',
  'bridge', 'pier', 'fence', 'sign', 'crate', 'well', 'barrel', 'chest',
  'houseRed', 'houseBlue', 'castle', 'roofPiece', 'stall', 'windowTiny', 'doorTiny', 'cart',
  'castleTower', 'gate', 'banner', 'torch', 'tableOut', 'chest2', 'crops', 'wheat',
  'cropTall', 'deadTree2', 'cactus', 'hay', 'crate2', 'coal', 'houseBrown', 'shop',
  'shed', 'tavern', 'market', 'barn', 'mill', 'well2', 'stonePile', 'grassTuft',
  'shrub', 'apple', 'pumpkin', 'cypress', 'dock', 'ore', 'flowers2', 'blank2'
];

function drawWorldObj(a, i, type) {
  if (type === 'blank' || type === 'blank2') return;
  if (type === 'tree') tree(a, i);
  else if (type === 'treeDark') tree(a, i, [43, 106, 45], [86, 142, 54]);
  else if (type === 'pine') pine(a, i);
  else if (type === 'pineBlue') pine(a, i, [65, 92, 133]);
  else if (type === 'bush') {
    ellipse(a, tx(i) + 16, ty(i) + 21, 12, 6, [45, 128, 46]);
    rect(a, tx(i) + 8, ty(i) + 14, 16, 8, [90, 169, 62]);
  } else if (type === 'flowers' || type === 'flowers2') {
    for (let k = 0; k < 7; k++) {
      rect(a, tx(i) + 5 + k * 3, ty(i) + 18 - (k % 2) * 5, 2, 2, [[240, 232, 92], [239, 112, 145], [245, 246, 246]][k % 3]);
      rect(a, tx(i) + 5 + k * 3, ty(i) + 20 - (k % 2) * 5, 1, 5, [41, 128, 44]);
    }
  } else if (type.startsWith('deadTree')) deadTree(a, i);
  else if (type === 'log') {
    rect(a, tx(i) + 7, ty(i) + 20, 19, 6, [116, 79, 42]);
    ellipse(a, tx(i) + 8, ty(i) + 21, 4, 5, [87, 54, 31]);
    ellipse(a, tx(i) + 24, ty(i) + 21, 4, 5, [87, 54, 31]);
  } else if (type === 'mountain') mountain(a, i);
  else if (type === 'snowMountain') mountain(a, i, [144, 147, 145], [246, 248, 245]);
  else if (type === 'darkMountain') mountain(a, i, [103, 86, 70], [206, 198, 176]);
  else if (type === 'rock') rockObj(a, i);
  else if (type === 'brownRock') rockObj(a, i, [154, 117, 76]);
  else if (type === 'hill') {
    tri(a, [tx(i) + 6, ty(i) + 27], [tx(i) + 15, ty(i) + 14], [tx(i) + 26, ty(i) + 27], [95, 74, 46]);
    line(a, tx(i) + 14, ty(i) + 16, tx(i) + 9, ty(i) + 26, [204, 170, 91]);
  } else if (type === 'swamp') {
    rect(a, tx(i) + 6, ty(i) + 16, 20, 12, [66, 92, 67]);
    speck(a, i, [39, 69, 53], [93, 129, 80], 20, 117);
  } else if (type === 'bridge' || type === 'dock') bridge(a, i);
  else if (type === 'pier') {
    rect(a, tx(i) + 5, ty(i) + 15, 22, 12, [81, 61, 40]);
    rect(a, tx(i) + 7, ty(i) + 11, 18, 3, [210, 171, 83]);
    for (let xx = 8; xx < 26; xx += 6) rect(a, tx(i) + xx, ty(i) + 13, 2, 12, [52, 40, 29]);
  } else if (type === 'fence') fence(a, i);
  else if (type === 'sign') sign(a, i);
  else if (type === 'crate' || type === 'crate2') crate(a, i);
  else if (type.startsWith('well')) well(a, i);
  else if (type === 'barrel') barrel(a, i);
  else if (type.startsWith('chest')) {
    rect(a, tx(i) + 8, ty(i) + 12, 16, 13, [118, 74, 39]);
    border(a, tx(i) + 8, ty(i) + 12, 16, 13, [70, 46, 31]);
    rect(a, tx(i) + 9, ty(i) + 10, 14, 3, [228, 178, 64]);
  } else if (type === 'houseRed') house(a, i, [180, 59, 49], [214, 181, 118]);
  else if (type === 'houseBlue') house(a, i, [67, 103, 169], [212, 206, 164]);
  else if (type === 'castle' || type === 'castleTower' || type === 'gate') castle(a, i);
  else if (type === 'banner') banner(a, i);
  else if (type === 'torch') {
    rect(a, tx(i) + 12, ty(i) + 9, 8, 17, [116, 80, 41]);
    ellipse(a, tx(i) + 16, ty(i) + 9, 7, 5, [242, 189, 62]);
    ellipse(a, tx(i) + 16, ty(i) + 10, 5, 3, [216, 87, 44]);
  } else if (type === 'crops') crops(a, i, [76, 151, 43]);
  else if (type === 'wheat') crops(a, i, [206, 181, 66]);
  else if (type === 'cropTall') {
    rect(a, tx(i) + 6, ty(i) + 10, 20, 18, [52, 132, 49]);
    for (let k = 0; k < 5; k++) rect(a, tx(i) + 8 + k * 4, ty(i) + 7 + (k % 2) * 2, 3, 20, [115, 184, 62]);
  } else if (type === 'cactus') {
    rect(a, tx(i) + 12, ty(i) + 10, 8, 17, [94, 137, 72]);
    for (let k = 0; k < 5; k++) line(a, tx(i) + 16, ty(i) + 12, tx(i) + 7 + k * 5, ty(i) + 7 + k * 3, [64, 100, 60], 2);
  } else if (type === 'hay') {
    rect(a, tx(i) + 9, ty(i) + 10, 14, 17, [204, 194, 120]);
    for (let k = 0; k < 4; k++) line(a, tx(i) + 10 + k * 3, ty(i) + 11, tx(i) + 10 + k * 3, ty(i) + 26, [132, 115, 65]);
  } else if (type === 'coal' || type === 'ore' || type === 'stonePile') rockObj(a, i, [113, 132, 138]);
  else if (type === 'grassTuft') {
    rect(a, tx(i) + 4, ty(i) + 22, 24, 5, [51, 121, 49]);
    for (let k = 0; k < 8; k++) rect(a, tx(i) + 4 + k * 3, ty(i) + 14 - (k % 2) * 3, 2, 10 + (k % 2) * 3, [98, 177, 64]);
  } else if (type === 'shrub') tree(a, i, [55, 128, 45], [105, 165, 58]);
  else if (type === 'apple') {
    tree(a, i, [48, 133, 50], [78, 169, 61]);
    rect(a, tx(i) + 11, ty(i) + 15, 2, 2, [216, 50, 45]);
    rect(a, tx(i) + 22, ty(i) + 13, 2, 2, [216, 50, 45]);
  } else if (type === 'pumpkin') {
    ellipse(a, tx(i) + 16, ty(i) + 19, 8, 6, [216, 112, 46]);
    rect(a, tx(i) + 15, ty(i) + 10, 4, 5, [64, 124, 51]);
  } else if (type === 'cypress') pine(a, i, [79, 137, 62]);
  else house(a, i, [148, 91, 41], [224, 201, 143]);
}

const townObjs = [
  'blank', 'door', 'ironDoor', 'window', 'litWindow', 'awningRed', 'awningBlue', 'shopFront',
  'houseFront', 'counter', 'shelf', 'display', 'bookcase', 'plantShelf', 'weaponRack', 'potionRack',
  'innSign', 'itemSign', 'bed', 'bench', 'chair', 'table', 'cabinet', 'dresser',
  'stove', 'bar', 'barrel', 'crate', 'marketBox', 'lamp', 'fountain', 'well',
  'pillar', 'castleWall', 'throne', 'blueBanner', 'redBanner', 'stairs', 'torch', 'gate',
  'tower', 'flag', 'armor', 'statue', 'altar', 'chest', 'rugStand', 'villagerProp',
  'flowerPot', 'shopTable', 'foodTable', 'bookTable', 'magicStand', 'weaponStand', 'armorStand', 'tavernTable',
  'kitchen', 'clock', 'sign', 'notice', 'candles', 'plant', 'vase', 'blank2'
];

function drawTownObj(a, i, type) {
  if (type === 'blank' || type === 'blank2') return;
  if (type === 'door') door(a, i);
  else if (type === 'ironDoor') door(a, i, [97, 61, 38]);
  else if (type === 'window') windowObj(a, i);
  else if (type === 'litWindow') windowObj(a, i, [124, 85, 45], [183, 224, 239]);
  else if (type === 'awningRed') awning(a, i);
  else if (type === 'awningBlue') awning(a, i, [52, 118, 166], [241, 241, 229]);
  else if (type === 'shopFront') house(a, i, [151, 44, 41], [224, 201, 143]);
  else if (type === 'houseFront') house(a, i, [108, 39, 36], [214, 185, 110]);
  else if (type === 'counter' || type === 'bar') counter(a, i);
  else if (type === 'shelf' || type === 'bookcase' || type === 'weaponRack' || type === 'potionRack') shelf(a, i);
  else if (type === 'display' || type === 'plantShelf') {
    counter(a, i);
    rect(a, tx(i) + 12, ty(i) + 10, 8, 5, type === 'plantShelf' ? [65, 132, 58] : [233, 205, 105]);
  } else if (type === 'innSign' || type === 'itemSign' || type === 'sign' || type === 'notice') sign(a, i);
  else if (type === 'bed') bed(a, i);
  else if (type === 'bench') fence(a, i);
  else if (type === 'chair') chair(a, i);
  else if (type === 'table' || type === 'tavernTable' || type === 'foodTable' || type === 'bookTable' || type === 'shopTable') table(a, i);
  else if (type === 'cabinet' || type === 'dresser') {
    rect(a, tx(i) + 9, ty(i) + 9, 14, 18, [108, 76, 48]);
    rect(a, tx(i) + 11, ty(i) + 13, 10, 10, [217, 177, 87]);
    rect(a, tx(i) + 10, ty(i) + 10, 12, 3, [72, 46, 32]);
  } else if (type === 'stove') castle(a, i);
  else if (type === 'barrel') barrel(a, i);
  else if (type === 'crate' || type === 'marketBox') crate(a, i);
  else if (type === 'lamp' || type === 'torch' || type === 'candles') {
    rect(a, tx(i) + 12, ty(i) + 9, 8, 17, [116, 80, 41]);
    ellipse(a, tx(i) + 16, ty(i) + 8, 7, 5, [242, 189, 62]);
  } else if (type === 'fountain' || type === 'well') well(a, i);
  else if (type === 'pillar' || type === 'statue') {
    rect(a, tx(i) + 9, ty(i) + 8, 14, 19, [109, 111, 116]);
    rect(a, tx(i) + 10, ty(i) + 5, 12, 4, [166, 168, 171]);
    rect(a, tx(i) + 11, ty(i) + 24, 10, 3, [72, 74, 79]);
  } else if (type === 'castleWall' || type === 'gate' || type === 'tower') castle(a, i);
  else if (type === 'throne' || type === 'altar') throne(a, i);
  else if (type === 'blueBanner') banner(a, i, [49, 82, 146]);
  else if (type === 'redBanner') banner(a, i, [131, 35, 47]);
  else if (type === 'stairs') stairs(a, i);
  else if (type === 'flag') banner(a, i, [118, 38, 48]);
  else if (type === 'armor' || type === 'armorStand') {
    rect(a, tx(i) + 8, ty(i) + 10, 16, 17, [116, 118, 123]);
    rect(a, tx(i) + 11, ty(i) + 16, 10, 11, [72, 75, 82]);
    rect(a, tx(i) + 10, ty(i) + 7, 12, 4, [164, 166, 170]);
  } else if (type === 'chest') drawWorldObj(a, i, 'chest');
  else if (type === 'rugStand') carpet(a, i, [38, 126, 126], [217, 183, 74]);
  else if (type === 'villagerProp' || type === 'flowerPot' || type === 'plant' || type === 'vase') {
    rect(a, tx(i) + 10, ty(i) + 18, 12, 8, [104, 71, 42]);
    rect(a, tx(i) + 12, ty(i) + 13, 8, 6, [65, 127, 61]);
    rect(a, tx(i) + 14, ty(i) + 10, 5, 4, [239, 112, 145]);
  } else if (type === 'magicStand') {
    counter(a, i);
    ellipse(a, tx(i) + 16, ty(i) + 10, 5, 5, [91, 54, 147]);
  } else if (type === 'weaponStand') shelf(a, i);
  else if (type === 'kitchen') {
    table(a, i);
    rect(a, tx(i) + 11, ty(i) + 10, 10, 5, [221, 188, 91]);
  } else if (type === 'clock') {
    rect(a, tx(i) + 12, ty(i) + 8, 8, 18, [83, 59, 41]);
    ellipse(a, tx(i) + 16, ty(i) + 9, 6, 4, [217, 180, 81]);
  } else table(a, i);
}

const outDir = path.join(__dirname, '..', 'resources', 'tiles');
fs.mkdirSync(outDir, { recursive: true });

const sheets = [
  ['base_world_tiles.bmp', img([20, 20, 20]), baseWorld, baseDraw],
  ['base_town_tiles.bmp', img([20, 20, 20]), baseTown, baseDraw],
  ['advanced_world_objects.bmp', img(MAG), worldObjs, drawWorldObj],
  ['advanced_town_objects.bmp', img(MAG), townObjs, drawTownObj],
];

for (const [name, im, list, draw] of sheets) {
  list.slice(0, 64).forEach((d, i) => draw(im, i, d));
  saveBmp(path.join(outDir, name), im);
}

for (const [name] of sheets) {
  const file = path.join(outDir, name);
  const b = fs.readFileSync(file);
  console.log(`${name}: ${b.length} bytes`);
}
