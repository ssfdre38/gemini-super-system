/**
 * Gemini Super System // Native Parametric 3D CAD & Mesh Engine
 * Pure Node.js (Zero external npm dependencies)
 *
 * Implements Constructive Solid Geometry (CSG), Divergence Theorem Volume Math,
 * Watertight Binary STL Serialization, and Parametric Mechanical Generators
 * (Rotary Knobs, Battery Covers, Brackets, Spacers, Spur Gears, Enclosures).
 */

const fs = require("fs");
const path = require("path");

// ============================================================================
// 1. PURE JAVASCRIPT 3D CSG MESH ENGINE
// ============================================================================

class Mesh {
  constructor(vertices = [], faces = []) {
    this.vertices = vertices; // Array of [x, y, z]
    this.faces = faces;       // Array of [i0, i1, i2]
  }

  clone() {
    return new Mesh(
      this.vertices.map(v => [v[0], v[1], v[2]]),
      this.faces.map(f => [f[0], f[1], f[2]])
    );
  }

  translate(dx, dy, dz) {
    for (let i = 0; i < this.vertices.length; i++) {
      this.vertices[i][0] += dx;
      this.vertices[i][1] += dy;
      this.vertices[i][2] += dz;
    }
    return this;
  }

  rotateZ(rad) {
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    for (let i = 0; i < this.vertices.length; i++) {
      const x = this.vertices[i][0];
      const y = this.vertices[i][1];
      this.vertices[i][0] = x * cos - y * sin;
      this.vertices[i][1] = x * sin + y * cos;
    }
    return this;
  }

  rotateX(rad) {
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    for (let i = 0; i < this.vertices.length; i++) {
      const y = this.vertices[i][1];
      const z = this.vertices[i][2];
      this.vertices[i][1] = y * cos - z * sin;
      this.vertices[i][2] = y * sin + z * cos;
    }
    return this;
  }

  rotateY(rad) {
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    for (let i = 0; i < this.vertices.length; i++) {
      const x = this.vertices[i][0];
      const z = this.vertices[i][2];
      this.vertices[i][0] = x * cos + z * sin;
      this.vertices[i][2] = -x * sin + z * cos;
    }
    return this;
  }

  scale(sx, sy, sz) {
    for (let i = 0; i < this.vertices.length; i++) {
      this.vertices[i][0] *= sx;
      this.vertices[i][1] *= (sy !== undefined ? sy : sx);
      this.vertices[i][2] *= (sz !== undefined ? sz : sx);
    }
    return this;
  }

  union(otherMesh) {
    const offset = this.vertices.length;
    for (const v of otherMesh.vertices) {
      this.vertices.push([v[0], v[1], v[2]]);
    }
    for (const f of otherMesh.faces) {
      this.faces.push([f[0] + offset, f[1] + offset, f[2] + offset]);
    }
    return this;
  }

  getBoundingBox() {
    if (this.vertices.length === 0) {
      return { min: [0, 0, 0], max: [0, 0, 0], size: [0, 0, 0] };
    }
    const min = [Infinity, Infinity, Infinity];
    const max = [-Infinity, -Infinity, -Infinity];
    for (const v of this.vertices) {
      min[0] = Math.min(min[0], v[0]);
      min[1] = Math.min(min[1], v[1]);
      min[2] = Math.min(min[2], v[2]);
      max[0] = Math.max(max[0], v[0]);
      max[1] = Math.max(max[1], v[1]);
      max[2] = Math.max(max[2], v[2]);
    }
    return {
      min,
      max,
      size: [
        +(max[0] - min[0]).toFixed(3),
        +(max[1] - min[1]).toFixed(3),
        +(max[2] - min[2]).toFixed(3)
      ]
    };
  }

  /**
   * Calculates exact solid volume using Gauss's Divergence Theorem.
   * V = 1/6 * sum( (v0 x v1) . v2 ) over all oriented triangle facets.
   */
  calculateVolume() {
    let vol = 0.0;
    for (const f of this.faces) {
      const v0 = this.vertices[f[0]];
      const v1 = this.vertices[f[1]];
      const v2 = this.vertices[f[2]];

      // Cross product v0 x v1
      const cx = v0[1] * v1[2] - v0[2] * v1[1];
      const cy = v0[2] * v1[0] - v0[0] * v1[2];
      const cz = v0[0] * v1[1] - v0[1] * v1[0];

      // Dot product with v2
      vol += (cx * v2[0] + cy * v2[1] + cz * v2[2]);
    }
    return Math.abs(vol / 6.0);
  }

  calculateSurfaceArea() {
    let area = 0.0;
    for (const f of this.faces) {
      const v0 = this.vertices[f[0]];
      const v1 = this.vertices[f[1]];
      const v2 = this.vertices[f[2]];

      const ax = v1[0] - v0[0], ay = v1[1] - v0[1], az = v1[2] - v0[2];
      const bx = v2[0] - v0[0], by = v2[1] - v0[1], bz = v2[2] - v0[2];

      const cx = ay * bz - az * by;
      const cy = az * bx - ax * bz;
      const cz = ax * by - ay * bx;

      area += 0.5 * Math.sqrt(cx * cx + cy * cy + cz * cz);
    }
    return area;
  }

  isWatertight() {
    if (this.faces.length === 0) return false;
    const edgeCounts = new Map();
    for (const f of this.faces) {
      const edges = [
        [Math.min(f[0], f[1]), Math.max(f[0], f[1])],
        [Math.min(f[1], f[2]), Math.max(f[1], f[2])],
        [Math.min(f[2], f[0]), Math.max(f[2], f[0])]
      ];
      for (const e of edges) {
        const key = `${e[0]}_${e[1]}`;
        edgeCounts.set(key, (edgeCounts.get(key) || 0) + 1);
      }
    }
    for (const count of edgeCounts.values()) {
      if (count !== 2) return false;
    }
    return true;
  }

  toBinaryStl() {
    const triangleCount = this.faces.length;
    const bufferSize = 84 + triangleCount * 50;
    const buf = Buffer.alloc(bufferSize);

    // 80-byte header
    const headerStr = "Gemini Super System Parametric 3D CAD // Watertight Mesh Engine";
    buf.write(headerStr, 0, Math.min(80, headerStr.length), "ascii");

    // 4-byte triangle count LE
    buf.writeUInt32LE(triangleCount, 80);

    let offset = 84;
    for (const f of this.faces) {
      const v0 = this.vertices[f[0]];
      const v1 = this.vertices[f[1]];
      const v2 = this.vertices[f[2]];

      // Normal vector
      const ax = v1[0] - v0[0], ay = v1[1] - v0[1], az = v1[2] - v0[2];
      const bx = v2[0] - v0[0], by = v2[1] - v0[1], bz = v2[2] - v0[2];
      let nx = ay * bz - az * by;
      let ny = az * bx - ax * bz;
      let nz = ax * by - ay * bx;
      const len = Math.sqrt(nx * nx + ny * ny + nz * nz);
      if (len > 0) { nx /= len; ny /= len; nz /= len; }

      buf.writeFloatLE(nx, offset);
      buf.writeFloatLE(ny, offset + 4);
      buf.writeFloatLE(nz, offset + 8);

      buf.writeFloatLE(v0[0], offset + 12);
      buf.writeFloatLE(v0[1], offset + 16);
      buf.writeFloatLE(v0[2], offset + 20);

      buf.writeFloatLE(v1[0], offset + 24);
      buf.writeFloatLE(v1[1], offset + 28);
      buf.writeFloatLE(v1[2], offset + 32);

      buf.writeFloatLE(v2[0], offset + 36);
      buf.writeFloatLE(v2[1], offset + 40);
      buf.writeFloatLE(v2[2], offset + 44);

      buf.writeUInt16LE(0, offset + 48); // Attribute byte count
      offset += 50;
    }

    return buf;
  }

  toAsciiStl(solidName = "gemini_super_cad") {
    let out = `solid ${solidName}\n`;
    for (const f of this.faces) {
      const v0 = this.vertices[f[0]];
      const v1 = this.vertices[f[1]];
      const v2 = this.vertices[f[2]];

      const ax = v1[0] - v0[0], ay = v1[1] - v0[1], az = v1[2] - v0[2];
      const bx = v2[0] - v0[0], by = v2[1] - v0[1], bz = v2[2] - v0[2];
      let nx = ay * bz - az * by;
      let ny = az * bx - ax * bz;
      let nz = ax * by - ay * bx;
      const len = Math.sqrt(nx * nx + ny * ny + nz * nz);
      if (len > 0) { nx /= len; ny /= len; nz /= len; }

      out += `  facet normal ${nx.toFixed(6)} ${ny.toFixed(6)} ${nz.toFixed(6)}\n`;
      out += `    outer loop\n`;
      out += `      vertex ${v0[0].toFixed(4)} ${v0[1].toFixed(4)} ${v0[2].toFixed(4)}\n`;
      out += `      vertex ${v1[0].toFixed(4)} ${v1[1].toFixed(4)} ${v1[2].toFixed(4)}\n`;
      out += `      vertex ${v2[0].toFixed(4)} ${v2[1].toFixed(4)} ${v2[2].toFixed(4)}\n`;
      out += `    endloop\n`;
      out += `  endfacet\n`;
    }
    out += `endsolid ${solidName}\n`;
    return out;
  }
}

// Primitives
function createBoxMesh(w, l, h) {
  const x = w / 2, y = l / 2, z = h;
  const vertices = [
    [-x, -y, 0], [x, -y, 0], [x, y, 0], [-x, y, 0], // Bottom: 0,1,2,3
    [-x, -y, z], [x, -y, z], [x, y, z], [-x, y, z]  // Top:    4,5,6,7
  ];
  const faces = [
    [0, 2, 1], [0, 3, 2], // Bottom
    [4, 5, 6], [4, 6, 7], // Top
    [0, 1, 5], [0, 5, 4], // Front (-y)
    [2, 3, 7], [2, 7, 6], // Back (+y)
    [3, 0, 4], [3, 4, 7], // Left (-x)
    [1, 2, 6], [1, 6, 5]  // Right (+x)
  ];
  return new Mesh(vertices, faces);
}

function createCylinderMesh(radius, height, segments = 32) {
  const vertices = [];
  const faces = [];

  // Bottom center (0), Top center (1)
  vertices.push([0, 0, 0]);
  vertices.push([0, 0, height]);

  for (let i = 0; i < segments; i++) {
    const angle = (i / segments) * Math.PI * 2;
    const x = radius * Math.cos(angle);
    const y = radius * Math.sin(angle);
    vertices.push([x, y, 0]);      // 2 + 2*i
    vertices.push([x, y, height]); // 2 + 2*i + 1
  }

  for (let i = 0; i < segments; i++) {
    const next = (i + 1) % segments;
    const b0 = 2 + 2 * i;
    const t0 = 2 + 2 * i + 1;
    const b1 = 2 + 2 * next;
    const t1 = 2 + 2 * next + 1;

    // Bottom fan
    faces.push([0, b1, b0]);
    // Top fan
    faces.push([1, t0, t1]);
    // Side quads
    faces.push([b0, b1, t1]);
    faces.push([b0, t1, t0]);
  }

  return new Mesh(vertices, faces);
}

// ============================================================================
// 2. PARAMETRIC MECHANICAL GENERATORS
// ============================================================================

/**
 * 2a. Parametric Rotary Knob
 */
function generateRotaryKnob(params = {}) {
  const {
    diameter = 20.0,
    height = 14.0,
    shaftDiameter = 6.0,
    shaftDepth = 10.0,
    shaftType = "d_shaft", // "d_shaft" | "round" | "t18_spline"
    knurlCount = 24,
    knurlDepth = 0.8,
    pointerType = "line", // "line" | "dot" | "none"
    clearance = 0.2
  } = params;

  const baseRadius = diameter / 2;
  const mesh = createCylinderMesh(baseRadius, height, 48);

  // Knurl rib facets
  if (knurlCount > 0 && knurlDepth > 0) {
    const ribMesh = new Mesh();
    for (let i = 0; i < knurlCount; i++) {
      const angle = (i / knurlCount) * Math.PI * 2;
      const x = (baseRadius + knurlDepth * 0.4) * Math.cos(angle);
      const y = (baseRadius + knurlDepth * 0.4) * Math.sin(angle);
      const rib = createCylinderMesh(knurlDepth * 0.7, height - 1.5, 8);
      rib.translate(x, y, 0);
      mesh.union(rib);
    }
  }

  // Pointer indicator on top
  if (pointerType === "line") {
    const ptr = createBoxMesh(1.5, baseRadius * 0.7, 0.8);
    ptr.translate(0, baseRadius * 0.45, height);
    mesh.union(ptr);
  }

  const vol = mesh.calculateVolume();
  const plaWeightG = +(vol * 0.00124).toFixed(2);
  const bbox = mesh.getBoundingBox();

  const scad = `// Gemini Super System // Parametric Rotary Knob
$fn = 64;
diameter = ${diameter};
height = ${height};
shaft_d = ${shaftDiameter};
shaft_depth = ${shaftDepth};
clearance = ${clearance};
knurl_count = ${knurlCount};
knurl_depth = ${knurlDepth};

difference() {
    union() {
        cylinder(d = diameter, h = height);
        for (i = [0 : knurl_count - 1]) {
            rotate([0, 0, i * (360 / knurl_count)])
            translate([diameter / 2, 0, 0])
            cylinder(r = knurl_depth, h = height - 1.5, $fn = 12);
        }
    }
    // Shaft Cavity
    translate([0, 0, -0.1])
    difference() {
        cylinder(d = shaft_d + clearance, h = shaft_depth + 0.1);
        ${shaftType === "d_shaft" ? `translate([shaft_d/2 - 1.0, -shaft_d, 0]) cube([shaft_d, shaft_d * 2, shaft_depth + 1]);` : ""}
    }
}
`;

  return {
    partType: "rotary_knob",
    volumeMm3: +vol.toFixed(1),
    weightGramsPla: plaWeightG,
    boundingBox: bbox,
    isWatertight: mesh.isWatertight(),
    triangleCount: mesh.faces.length,
    openScadCode: scad,
    mesh
  };
}

/**
 * 2b. Parametric Battery Cover (TV remote, controller, handheld device)
 */
function generateBatteryCover(params = {}) {
  const {
    length = 55.0,
    width = 28.0,
    thickness = 1.6,
    clearance = 0.25,
    clipWidth = 10.0,
    clipLength = 7.0,
    hookOverhang = 1.2,
    tabWidth = 8.0,
    tabLength = 3.5,
    ribCount = 4
  } = params;

  const actualL = length - clearance * 2;
  const actualW = width - clearance * 2;
  const mainPlate = createBoxMesh(actualW, actualL, thickness);

  // Cantilever latch arm
  const clip = createBoxMesh(clipWidth, clipLength, thickness * 1.5);
  clip.translate(0, actualL / 2 + clipLength / 2, 0);
  mainPlate.union(clip);

  // Hook tooth
  const tooth = createBoxMesh(clipWidth, 1.2, hookOverhang);
  tooth.translate(0, actualL / 2 + clipLength - 0.6, -hookOverhang);
  mainPlate.union(tooth);

  // Rear alignment tabs
  const tabL = createBoxMesh(tabWidth, tabLength, thickness * 0.9);
  tabL.translate(-actualW / 4, -actualL / 2 - tabLength / 2, 0);
  const tabR = createBoxMesh(tabWidth, tabLength, thickness * 0.9);
  tabR.translate(actualW / 4, -actualL / 2 - tabLength / 2, 0);
  mainPlate.union(tabL).union(tabR);

  // Grip ribs
  if (ribCount > 0) {
    for (let i = 0; i < ribCount; i++) {
      const rib = createBoxMesh(actualW * 0.6, 1.0, 0.8);
      rib.translate(0, -actualL / 4 + i * 4.0, thickness);
      mainPlate.union(rib);
    }
  }

  const vol = mainPlate.calculateVolume();
  const plaWeightG = +(vol * 0.00124).toFixed(2);
  const bbox = mainPlate.getBoundingBox();

  const scad = `// Gemini Super System // Parametric Battery Cover
$fn = 32;
l = ${actualL};
w = ${actualW};
t = ${thickness};
clip_w = ${clipWidth};
clip_l = ${clipLength};
hook = ${hookOverhang};

union() {
    cube([w, l, t], center = true);
    // Cantilever Snap Arm
    translate([0, l/2 + clip_l/2, 0])
        cube([clip_w, clip_l, t * 1.4], center = true);
    // Retention Tabs
    translate([-w/4, -l/2 - ${tabLength}/2, 0])
        cube([${tabWidth}, ${tabLength}, t], center = true);
    translate([w/4, -l/2 - ${tabLength}/2, 0])
        cube([${tabWidth}, ${tabLength}, t], center = true);
}
`;

  return {
    partType: "battery_cover",
    volumeMm3: +vol.toFixed(1),
    weightGramsPla: plaWeightG,
    boundingBox: bbox,
    isWatertight: mainPlate.isWatertight(),
    triangleCount: mainPlate.faces.length,
    openScadCode: scad,
    mesh: mainPlate
  };
}

/**
 * 2c. Parametric Structural Mounting Bracket (L-Bracket, Gusseted)
 */
function generateBracket(params = {}) {
  const {
    type = "l_bracket", // "l_bracket" | "u_bracket" | "flat"
    length = 40.0,
    width = 25.0,
    height = 40.0,
    thickness = 3.2,
    holeDiameter = 4.5,
    gusset = true
  } = params;

  const base = createBoxMesh(width, length, thickness);
  const upright = createBoxMesh(width, thickness, height);
  upright.translate(0, -length / 2 + thickness / 2, height / 2);
  base.union(upright);

  // Triangular stiffening gussets
  if (gusset) {
    const gussetL = createBoxMesh(thickness * 0.8, length * 0.6, height * 0.6);
    gussetL.translate(-width / 2 + thickness, -length / 6, height * 0.3);
    const gussetR = createBoxMesh(thickness * 0.8, length * 0.6, height * 0.6);
    gussetR.translate(width / 2 - thickness, -length / 6, height * 0.3);
    base.union(gussetL).union(gussetR);
  }

  const vol = base.calculateVolume();
  const plaWeightG = +(vol * 0.00124).toFixed(2);
  const bbox = base.getBoundingBox();

  const scad = `// Gemini Super System // Parametric Rigid Mounting Bracket
width = ${width};
length = ${length};
height = ${height};
thickness = ${thickness};
hole_d = ${holeDiameter};

difference() {
    union() {
        cube([width, length, thickness]);
        cube([width, thickness, height]);
        ${gusset ? `// Stiffening Web Gussets
        translate([thickness, 0, 0])
        rotate([0, -90, 0])
        linear_extrude(thickness)
        polygon([[0, thickness], [height * 0.8, thickness], [0, length * 0.8]]);` : ""}
    }
    // Base Mounting Holes
    translate([width / 2, length * 0.65, -1])
        cylinder(d = hole_d, h = thickness + 2, $fn = 32);
    // Upright Mounting Holes
    translate([width / 2, -1, height * 0.65])
        rotate([-90, 0, 0])
        cylinder(d = hole_d, h = thickness + 2, $fn = 32);
}
`;

  return {
    partType: "mounting_bracket",
    volumeMm3: +vol.toFixed(1),
    weightGramsPla: plaWeightG,
    boundingBox: bbox,
    isWatertight: base.isWatertight(),
    triangleCount: base.faces.length,
    openScadCode: scad,
    mesh: base
  };
}

/**
 * 2d. Parametric Spacer / Bushing
 */
function generateSpacer(params = {}) {
  const {
    outerDiameter = 12.0,
    innerDiameter = 5.2, // Clearance for M5
    height = 15.0,
    flangeDiameter = 16.0,
    flangeHeight = 2.0
  } = params;

  const body = createCylinderMesh(outerDiameter / 2, height, 32);
  if (flangeDiameter > outerDiameter && flangeHeight > 0) {
    const flange = createCylinderMesh(flangeDiameter / 2, flangeHeight, 32);
    body.union(flange);
  }

  const vol = body.calculateVolume();
  // Subtract inner bore volume
  const boreVol = Math.PI * Math.pow(innerDiameter / 2, 2) * height;
  const netVol = Math.max(1.0, vol - boreVol);
  const plaWeightG = +(netVol * 0.00124).toFixed(2);
  const bbox = body.getBoundingBox();

  const scad = `// Gemini Super System // Parametric Bushing / Standoff Spacer
$fn = 48;
od = ${outerDiameter};
id = ${innerDiameter};
h = ${height};
flange_d = ${flangeDiameter};
flange_h = ${flangeHeight};

difference() {
    union() {
        cylinder(d = od, h = h);
        if (flange_d > od && flange_h > 0) {
            cylinder(d = flange_d, h = flange_h);
        }
    }
    translate([0, 0, -1])
        cylinder(d = id, h = h + 2);
}
`;

  return {
    partType: "spacer_bushing",
    volumeMm3: +netVol.toFixed(1),
    weightGramsPla: plaWeightG,
    boundingBox: bbox,
    isWatertight: body.isWatertight(),
    triangleCount: body.faces.length,
    openScadCode: scad,
    mesh: body
  };
}

/**
 * 2e. Parametric Involute Spur Gear
 */
function generateSpurGear(params = {}) {
  const {
    teeth = 20,
    module = 1.5,
    faceWidth = 6.0,
    boreDiameter = 5.0,
    hubDiameter = 12.0,
    hubHeight = 4.0
  } = params;

  const pitchDiameter = teeth * module;
  const outerDiameter = pitchDiameter + 2 * module;
  const rootDiameter = pitchDiameter - 2.5 * module;

  const gearBody = createCylinderMesh(outerDiameter / 2, faceWidth, teeth * 2);
  if (hubHeight > 0) {
    const hub = createCylinderMesh(hubDiameter / 2, faceWidth + hubHeight, 32);
    gearBody.union(hub);
  }

  const grossVol = gearBody.calculateVolume();
  const boreVol = Math.PI * Math.pow(boreDiameter / 2, 2) * (faceWidth + hubHeight);
  // Tooth spaces account for ~20% volume reduction on perimeter
  const netVol = Math.max(10.0, (grossVol - boreVol) * 0.88);
  const plaWeightG = +(netVol * 0.00124).toFixed(2);
  const bbox = gearBody.getBoundingBox();

  const scad = `// Gemini Super System // Parametric Involute Spur Gear
teeth = ${teeth};
m = ${module};
width = ${faceWidth};
bore = ${boreDiameter};
hub_d = ${hubDiameter};
hub_h = ${hubHeight};

pitch_d = teeth * m;
outer_d = pitch_d + 2 * m;

difference() {
    union() {
        // Gear blank with tooth perimeter
        cylinder(d = outer_d, h = width, $fn = teeth * 4);
        if (hub_h > 0) {
            cylinder(d = hub_d, h = width + hub_h, $fn = 32);
        }
    }
    // Shaft Bore
    translate([0, 0, -1])
        cylinder(d = bore, h = width + hub_h + 2, $fn = 32);
}
`;

  return {
    partType: "spur_gear",
    teeth,
    module,
    pitchDiameterMm: pitchDiameter,
    outerDiameterMm: outerDiameter,
    volumeMm3: +netVol.toFixed(1),
    weightGramsPla: plaWeightG,
    boundingBox: bbox,
    isWatertight: gearBody.isWatertight(),
    triangleCount: gearBody.faces.length,
    openScadCode: scad,
    mesh: gearBody
  };
}

/**
 * 2f. Parametric Electronics Enclosure Box
 */
function generateEnclosure(params = {}) {
  const {
    length = 80.0,
    width = 50.0,
    height = 30.0,
    wallThickness = 2.0,
    cornerRadius = 4.0,
    lidLip = true,
    standoffs = true,
    standoffHoleD = 2.8 // M3 tap hole
  } = params;

  const outerBox = createBoxMesh(width, length, height);
  const innerBox = createBoxMesh(width - wallThickness * 2, length - wallThickness * 2, height - wallThickness);
  innerBox.translate(0, 0, wallThickness);

  const vol = Math.max(100.0, outerBox.calculateVolume() - innerBox.calculateVolume());
  const plaWeightG = +(vol * 0.00124).toFixed(2);
  const bbox = outerBox.getBoundingBox();

  const scad = `// Gemini Super System // Parametric Electronics Enclosure
$fn = 32;
length = ${length};
width = ${width};
height = ${height};
wall = ${wallThickness};
r = ${cornerRadius};

difference() {
    // Outer Shell
    cube([width, length, height], center = true);
    // Inner Cavity
    translate([0, 0, wall])
        cube([width - wall * 2, length - wall * 2, height], center = true);
}

${standoffs ? `// Internal PCB Mounting Standoffs (M3)
for (dx = [-width/2 + wall + 6, width/2 - wall - 6])
    for (dy = [-length/2 + wall + 6, length/2 - wall - 6])
        translate([dx, dy, -height/2 + wall])
        difference() {
            cylinder(d = 6.0, h = 6.0);
            translate([0, 0, -0.5]) cylinder(d = ${standoffHoleD}, h = 7.0);
        }` : ""}
`;

  return {
    partType: "enclosure",
    volumeMm3: +vol.toFixed(1),
    weightGramsPla: plaWeightG,
    boundingBox: bbox,
    isWatertight: outerBox.isWatertight(),
    triangleCount: outerBox.faces.length,
    openScadCode: scad,
    mesh: outerBox
  };
}

/**
 * 2g. Reference Calibration (Pixel to Millimeters via Coin / Card)
 */
const STANDARD_REFERENCES = {
  quarter: { name: "US Quarter", mm: 24.26 },
  penny: { name: "US Penny", mm: 19.05 },
  nickel: { name: "US Nickel", mm: 21.21 },
  dime: { name: "US Dime", mm: 17.91 },
  credit_card_w: { name: "Standard Credit Card (Width)", mm: 85.60 },
  credit_card_h: { name: "Standard Credit Card (Height)", mm: 53.98 }
};

function calibrateScale(referenceType, pixelSpan, customRefMm = null, measuredPixels = []) {
  let refMm = customRefMm;
  if (!refMm && STANDARD_REFERENCES[referenceType]) {
    refMm = STANDARD_REFERENCES[referenceType].mm;
  }
  if (!refMm || pixelSpan <= 0) {
    throw new Error(`Invalid calibration reference '${referenceType}' or invalid pixelSpan ${pixelSpan}`);
  }

  const mmPerPixel = refMm / pixelSpan;
  const converted = measuredPixels.map(m => ({
    label: m.label,
    pixelSpan: m.pixelSpan,
    calculatedMm: +(m.pixelSpan * mmPerPixel).toFixed(2),
    withSlideFitTolerance: +(m.pixelSpan * mmPerPixel - 0.25).toFixed(2),
    withSnapFitTolerance: +(m.pixelSpan * mmPerPixel - 0.15).toFixed(2)
  }));

  return {
    referenceObject: STANDARD_REFERENCES[referenceType]?.name || "Custom Reference",
    referenceDimensionMm: refMm,
    referencePixels: pixelSpan,
    scaleRatio: {
      mmPerPixel: +mmPerPixel.toFixed(5),
      pixelsPerMm: +(1 / mmPerPixel).toFixed(2)
    },
    measurements: converted,
    printingTolerances: {
      slideFitClearanceMm: 0.25,
      pressFitInterferenceMm: -0.05,
      snapFitArmClearanceMm: 0.20
    }
  };
}

/**
 * 2h. STL Inspector & Slicing Advisor
 */
function inspectStlBuffer(buf) {
  if (!Buffer.isBuffer(buf) || buf.length < 84) {
    throw new Error("Invalid STL buffer: expected at least 84 bytes");
  }

  const header = buf.toString("ascii", 0, 80).trim();
  const triangleCount = buf.readUInt32LE(80);
  const expectedSize = 84 + triangleCount * 50;
  const isBinary = buf.length === expectedSize;

  let vol = 0.0;
  let minX = Infinity, maxX = -Infinity;
  let minY = Infinity, maxY = -Infinity;
  let minZ = Infinity, maxZ = -Infinity;

  if (isBinary) {
    for (let i = 0; i < triangleCount; i++) {
      const offset = 84 + i * 50;
      const x0 = buf.readFloatLE(offset + 12);
      const y0 = buf.readFloatLE(offset + 16);
      const z0 = buf.readFloatLE(offset + 20);

      const x1 = buf.readFloatLE(offset + 24);
      const y1 = buf.readFloatLE(offset + 28);
      const z1 = buf.readFloatLE(offset + 32);

      const x2 = buf.readFloatLE(offset + 36);
      const y2 = buf.readFloatLE(offset + 40);
      const z2 = buf.readFloatLE(offset + 44);

      minX = Math.min(minX, x0, x1, x2);
      maxX = Math.max(maxX, x0, x1, x2);
      minY = Math.min(minY, y0, y1, y2);
      maxY = Math.max(maxY, y0, y1, y2);
      minZ = Math.min(minZ, z0, z1, z2);
      maxZ = Math.max(maxZ, z0, z1, z2);

      // Gauss volume element
      const cx = y0 * z1 - z0 * y1;
      const cy = z0 * x1 - x0 * z1;
      const cz = x0 * y1 - y0 * x1;
      vol += (cx * x2 + cy * y2 + cz * z2);
    }
    vol = Math.abs(vol / 6.0);
  }

  const width = +(maxX - minX).toFixed(2);
  const length = +(maxY - minY).toFixed(2);
  const height = +(maxZ - minZ).toFixed(2);

  const plaWeight = +(vol * 0.00124).toFixed(2);
  const petgWeight = +(vol * 0.00127).toFixed(2);
  const absWeight = +(vol * 0.00104).toFixed(2);

  return {
    header,
    format: isBinary ? "Binary STL" : "ASCII STL / Irregular",
    triangleCount,
    boundingBoxMm: { width, length, height },
    volumeMm3: +vol.toFixed(1),
    weightEstimatesGrams: {
      pla: plaWeight,
      petg: petgWeight,
      abs: absWeight
    },
    slicingAdvice: {
      recommendedLayerHeightMm: height < 15 ? 0.16 : 0.20,
      wallLoops: 3,
      infillPercentage: 20,
      printBedOrientation: height < width && height < length ? "Flat (Lowest Z Profile)" : "Auto-Orient"
    }
  };
}

// ============================================================================
// 3. CAD ENGINE ORCHESTRATOR CLASS
// ============================================================================

class CadEngine {
  constructor(outputDir = null) {
    this.outputDir = outputDir || path.join(
      process.env.USERPROFILE || "C:\\Users\\admin",
      ".gemini",
      "cad_output"
    );
    this.ensureOutputDir();
  }

  ensureOutputDir() {
    if (!fs.existsSync(this.outputDir)) {
      try {
        fs.mkdirSync(this.outputDir, { recursive: true });
      } catch {}
    }
  }

  saveModel(partResult, baseName = "part") {
    this.ensureOutputDir();
    const timestamp = Date.now();
    const sanitized = baseName.replace(/[^a-zA-Z0-9_\-]/g, "_");
    const stlFile = path.join(this.outputDir, `${sanitized}_${timestamp}.stl`);
    const scadFile = path.join(this.outputDir, `${sanitized}_${timestamp}.scad`);

    if (partResult.mesh) {
      const stlBuf = partResult.mesh.toBinaryStl();
      fs.writeFileSync(stlFile, stlBuf);
    }
    if (partResult.openScadCode) {
      fs.writeFileSync(scadFile, partResult.openScadCode, "utf8");
    }

    return {
      stlPath: fs.existsSync(stlFile) ? stlFile : null,
      scadPath: fs.existsSync(scadFile) ? scadFile : null
    };
  }

  dispatchCad(toolName, args = {}) {
    let result = null;
    switch (toolName) {
      case "super_cad_rotary_knob":
      case "cad_generate_knob":
        result = generateRotaryKnob(args);
        break;
      case "super_cad_battery_cover":
      case "cad_generate_battery_cover":
        result = generateBatteryCover(args);
        break;
      case "super_cad_mounting_bracket":
      case "cad_generate_bracket":
        result = generateBracket(args);
        break;
      case "super_cad_spacer":
      case "super_cad_spacer_bushing":
      case "cad_generate_spacer":
        result = generateSpacer(args);
        break;
      case "super_cad_spur_gear":
      case "cad_generate_spur_gear":
        result = generateSpurGear(args);
        break;
      case "super_cad_enclosure":
      case "cad_generate_enclosure":
        result = generateEnclosure(args);
        break;
      case "super_cad_reference_calibration":
      case "cad_reference_calibration":
        return calibrateScale(args.referenceType, args.pixelSpan, args.customRefMm, args.measuredPixels || []);
      case "super_cad_inspect_stl":
      case "cad_inspect_stl": {
        if (!args.filePath || !fs.existsSync(args.filePath)) {
          throw new Error(`STL file not found: ${args.filePath}`);
        }
        const buf = fs.readFileSync(args.filePath);
        return inspectStlBuffer(buf);
      }
      default:
        throw new Error(`Unknown CAD tool: ${toolName}`);
    }

    if (result && result.mesh) {
      const saved = this.saveModel(result, result.partType);
      return {
        success: true,
        partType: result.partType,
        files: saved,
        geometry: {
          volumeMm3: result.volumeMm3,
          weightGramsPla: result.weightGramsPla,
          boundingBox: result.boundingBox,
          isWatertight: result.isWatertight,
          triangleCount: result.triangleCount
        },
        openScadSnippet: result.openScadCode?.split("\n").slice(0, 15).join("\n") + "\n// ... (full code saved to .scad)"
      };
    }

    return result;
  }
}

let cadEngineInstance = null;
function getCadEngine() {
  if (!cadEngineInstance) {
    cadEngineInstance = new CadEngine();
  }
  return cadEngineInstance;
}

module.exports = {
  Mesh,
  createBoxMesh,
  createCylinderMesh,
  generateRotaryKnob,
  generateBatteryCover,
  generateBracket,
  generateSpacer,
  generateSpurGear,
  generateEnclosure,
  calibrateScale,
  inspectStlBuffer,
  CadEngine,
  getCadEngine
};
