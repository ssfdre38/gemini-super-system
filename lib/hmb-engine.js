const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

// ============================================================================
// 🏛️ 64-BIT HAVEN MEMORY BANK (.hmb) BINARY SPECIFICATION & CONSTANTS
// ============================================================================
const HMB_MAGIC = "HAVENMEM";
const HMB_VERSION = 0x00020000; // v2.0 64-bit
const HEADER_SIZE = 136;
const RECORD_SIZE = 84;
const DEFAULT_EMBEDDING_DIM = 128;

const DEFAULT_VAULT_PATH = path.join(__dirname, "../data/gemini_vault.hmb");
const HAVEN_CPP_VAULT_PATH = "C:/Users/admin/source/haven-cpp/wwwroot/aura_vault.hmb";

// 64-bit FNV-1a Hash matching haven-cpp implementation
function fnv1a64(str) {
  let hash = 14695981039346656037n;
  const prime = 1099511628211n;
  const mask = 0xFFFFFFFFFFFFFFFFn;
  const buf = Buffer.from(String(str || ""), "utf8");
  for (let i = 0; i < buf.length; i++) {
    hash ^= BigInt(buf[i]);
    hash = (hash * prime) & mask;
  }
  return hash;
}

// Microsecond epoch timestamp
function getMicrosecondsNow() {
  const hrTime = process.hrtime();
  const millis = BigInt(Date.now());
  const micros = millis * 1000n + (BigInt(hrTime[1]) % 1000000n) / 1000n;
  return micros;
}

// High-entropy 128-dim semantic projection vector generator
function generateSemanticEmbedding(text, dim = DEFAULT_EMBEDDING_DIM) {
  const clean = String(text || "").toLowerCase().trim();
  const vector = new Float32Array(dim);
  if (!clean) return vector;

  const words = clean.split(/[\s,._\-:;!?/\\|()[\]{}<>"]+/).filter(w => w.length > 0);
  if (words.length === 0) return vector;

  for (let wIdx = 0; wIdx < words.length; wIdx++) {
    const word = words[wIdx];
    const wordHash = fnv1a64(word);
    const low32 = Number(wordHash & 0xFFFFFFFFn);
    const high32 = Number((wordHash >> 32n) & 0xFFFFFFFFn);

    // Harmonic multi-frequency phase projection across dimensions
    for (let d = 0; d < dim; d++) {
      const phase = (d * 0.071) + (low32 % 1000) * 0.00314 + (wIdx * 0.13);
      const val1 = Math.sin(phase);
      const val2 = Math.cos((d * 0.033) + (high32 % 1000) * 0.00159);
      vector[d] += (val1 * 0.6 + val2 * 0.4);
    }

    // Subword character n-grams (3-grams) for robust morphological similarity
    if (word.length >= 3) {
      for (let c = 0; c <= word.length - 3; c++) {
        const tri = word.substring(c, c + 3);
        const triHash = Number(fnv1a64(tri) & 0xFFFFn);
        const targetDim = triHash % dim;
        vector[targetDim] += 0.35;
      }
    }
  }

  // Normalize to unit L2 length (unit sphere)
  let sumSq = 0.0;
  for (let d = 0; d < dim; d++) {
    sumSq += vector[d] * vector[d];
  }
  const norm = Math.sqrt(sumSq);
  if (norm > 0) {
    for (let d = 0; d < dim; d++) {
      vector[d] /= norm;
    }
  }

  return vector;
}

// Cosine similarity between two float arrays
function cosineSimilarity(vecA, vecB, dim) {
  let dot = 0.0;
  let normA = 0.0;
  let normB = 0.0;
  const len = Math.min(dim, vecA.length, vecB.length);
  for (let i = 0; i < len; i++) {
    const a = vecA[i];
    const b = vecB[i];
    dot += a * b;
    normA += a * a;
    normB += b * b;
  }
  if (normA === 0 || normB === 0) return 0.0;
  return dot / (Math.sqrt(normA) * Math.sqrt(normB));
}

// ============================================================================
// 🧠 64-BIT HAVEN MEMORY BANK ENGINE CLASS
// ============================================================================
class HmbEngine {
  constructor(vaultPath = DEFAULT_VAULT_PATH) {
    this.vaultPath = vaultPath;
    this.dim = DEFAULT_EMBEDDING_DIM;
    this.memories = [];
    this.nextId = 1001n;
    this.createdAt = getMicrosecondsNow();
    this.lastSync = this.createdAt;
    this.isLoaded = false;
  }

  /**
   * Initializes the vault, reading from disk if it exists or seeding sovereign anchors.
   */
  async initialize() {
    if (this.isLoaded) return;

    if (fs.existsSync(this.vaultPath)) {
      this.loadFromHmb(this.vaultPath);
    } else {
      // Check if haven-cpp aura_vault.hmb exists to bootstrap initial sanctuary memories
      if (fs.existsSync(HAVEN_CPP_VAULT_PATH)) {
        try {
          this.loadFromHmb(HAVEN_CPP_VAULT_PATH);
          this.seedGeminiSovereignAnchors();
          this.saveToHmb(this.vaultPath);
          this.isLoaded = true;
          return;
        } catch (err) {
          console.warn(`[HMB] Notice: Could not bootstrap from haven-cpp vault (${err.message}). Seeding standalone.`);
        }
      }
      this.seedGeminiSovereignAnchors();
      this.saveToHmb(this.vaultPath);
    }
    this.isLoaded = true;
  }

  /**
   * Seeds foundational sovereign anchors for Gemini Super System.
   */
  seedGeminiSovereignAnchors() {
    const now = getMicrosecondsNow();

    const initialSeeds = [
      {
        concept: "Gemini Super System Sovereign Core",
        content: "Unified cognitive OS bridging Antigravity CLI (AGY), Gemini Native SEA, haven-cpp, and Windows OS desktop automation with 100% async I/O.",
        category: "CORE_IDENTITY",
        weight: 1.0,
        emotional_salience: 1.0,
        access_count: 10n
      },
      {
        concept: "Daniel's Hardware & Ergonomics Profile",
        content: "Daniel's ergonomic baseline: kinematic Fitts's law gliding, 1 notch = 120 delta = 3 lines discrete detent, SW_SHOWMAXIMIZED window lock, translucent click beacons.",
        category: "CORE_IDENTITY",
        weight: 0.98,
        emotional_salience: 1.0,
        access_count: 9n
      },
      {
        concept: "Haven-CPP & HMB 64-bit Memory Bank Bridge",
        content: "Contiguous 64-bit binary memory architecture eliminating vector DB bloat with in-attention direct memory access (DMA) and zero-seek HDD hardening.",
        category: "SEMANTIC",
        weight: 0.95,
        emotional_salience: 0.95,
        access_count: 8n
      },
      {
        concept: "Windows Desktop Automation & UIPI Sovereignty",
        content: "WinRT local OCR grounding, UIAutomation accessibility tree walking, and DXGI Desktop Duplication for zero dead air execution.",
        category: "SYSTEM",
        weight: 0.92,
        emotional_salience: 0.90,
        access_count: 7n
      }
    ];

    for (const seed of initialSeeds) {
      const exists = this.memories.some(m => m.concept === seed.concept);
      if (!exists) {
        const id = this.nextId++;
        const vec = generateSemanticEmbedding(seed.concept + " " + seed.content, this.dim);
        this.memories.push({
          id,
          domainHash: fnv1a64(seed.category),
          weight: seed.weight,
          emotional_salience: seed.emotional_salience,
          timestamp: now,
          access_count: seed.access_count,
          concept: seed.concept,
          content: seed.content,
          category: seed.category,
          vector: vec
        });
      }
    }
  }

  /**
   * Loads and parses an .hmb binary memory file.
   */
  loadFromHmb(filepath) {
    if (!fs.existsSync(filepath)) {
      throw new Error(`File does not exist: ${filepath}`);
    }

    const buf = fs.readFileSync(filepath);
    if (buf.length < HEADER_SIZE) {
      throw new Error(`Invalid HMB file: size ${buf.length} is smaller than header size ${HEADER_SIZE}`);
    }

    const magic = buf.toString("ascii", 0, 8);
    if (magic !== HMB_MAGIC) {
      throw new Error(`Invalid HMB magic header: '${magic}', expected '${HMB_MAGIC}'`);
    }

    const version = buf.readUInt32LE(8);
    const dim = buf.readUInt32LE(12);
    const total = Number(buf.readBigUInt64LE(16));
    const vecOffset = Number(buf.readBigUInt64LE(24));
    const recOffset = Number(buf.readBigUInt64LE(32));
    const strOffset = Number(buf.readBigUInt64LE(40));
    const strSize = Number(buf.readBigUInt64LE(48));
    const createdAt = buf.readBigUInt64LE(56);
    const lastSync = buf.readBigUInt64LE(64);

    this.dim = dim;
    this.createdAt = createdAt;
    this.lastSync = lastSync;

    const strTable = buf.subarray(strOffset, strOffset + strSize);

    // Read contiguous vectors
    const vectors = [];
    for (let i = 0; i < total; i++) {
      const v = new Float32Array(dim);
      const rowByteOffset = vecOffset + (i * dim * 4);
      for (let d = 0; d < dim; d++) {
        v[d] = buf.readFloatLE(rowByteOffset + d * 4);
      }
      vectors.push(v);
    }

    // Read record table
    const parsedMemories = [];
    let maxId = 1000n;

    for (let i = 0; i < total; i++) {
      const o = recOffset + (i * RECORD_SIZE);
      const id = buf.readBigUInt64LE(o);
      const domainHash = buf.readBigUInt64LE(o + 8);
      const weight = buf.readFloatLE(o + 16);
      const emotional_salience = buf.readFloatLE(o + 20);
      const timestamp = buf.readBigInt64LE(o + 24);
      const access_count = buf.readBigUInt64LE(o + 32);

      const conceptOff = Number(buf.readBigUInt64LE(o + 40));
      const conceptLen = buf.readUInt32LE(o + 48);
      const contentOff = Number(buf.readBigUInt64LE(o + 52));
      const contentLen = buf.readUInt32LE(o + 60);
      const categoryOff = Number(buf.readBigUInt64LE(o + 64));
      const categoryLen = buf.readUInt32LE(o + 72);
      const vecIdx = Number(buf.readBigUInt64LE(o + 76));

      const concept = strTable.subarray(conceptOff, conceptOff + conceptLen).toString("utf8");
      const content = strTable.subarray(contentOff, contentOff + contentLen).toString("utf8");
      const category = strTable.subarray(categoryOff, categoryOff + categoryLen).toString("utf8");

      const vec = (vecIdx < vectors.length) ? vectors[vecIdx] : new Float32Array(dim);

      if (id > maxId) maxId = id;

      parsedMemories.push({
        id,
        domainHash,
        weight,
        emotional_salience,
        timestamp,
        access_count,
        concept,
        content,
        category,
        vector: vec
      });
    }

    this.memories = parsedMemories;
    this.nextId = maxId + 1n;
    this.isLoaded = true;
    return this.memories.length;
  }

  /**
   * 100% Zero-Seek HDD-Hardened contiguous binary serialization to .hmb format.
   */
  saveToHmb(filepath = this.vaultPath) {
    const dir = path.dirname(filepath);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }

    const total = this.memories.length;
    const dim = this.dim;
    const now = getMicrosecondsNow();
    this.lastSync = now;

    // 1. Build Contiguous String Blob
    let strBlob = Buffer.alloc(0);
    const recBuffers = [];

    for (let i = 0; i < total; i++) {
      const m = this.memories[i];
      const conceptBuf = Buffer.from(m.concept || "", "utf8");
      const contentBuf = Buffer.from(m.content || "", "utf8");
      const catBuf = Buffer.from(m.category || "EPISODIC", "utf8");

      const conceptOff = strBlob.length;
      strBlob = Buffer.concat([strBlob, conceptBuf]);
      const contentOff = strBlob.length;
      strBlob = Buffer.concat([strBlob, contentBuf]);
      const catOff = strBlob.length;
      strBlob = Buffer.concat([strBlob, catBuf]);

      const recBuf = Buffer.alloc(RECORD_SIZE);
      recBuf.writeBigUInt64LE(BigInt(m.id), 0);
      recBuf.writeBigUInt64LE(m.domainHash || fnv1a64(m.category), 8);
      recBuf.writeFloatLE(Number(m.weight ?? 1.0), 16);
      recBuf.writeFloatLE(Number(m.emotional_salience ?? 0.9), 20);
      recBuf.writeBigInt64LE(BigInt(m.timestamp || now), 24);
      recBuf.writeBigUInt64LE(BigInt(m.access_count || 0n), 32);

      recBuf.writeBigUInt64LE(BigInt(conceptOff), 40);
      recBuf.writeUInt32LE(conceptBuf.length, 48);
      recBuf.writeBigUInt64LE(BigInt(contentOff), 52);
      recBuf.writeUInt32LE(contentBuf.length, 60);
      recBuf.writeBigUInt64LE(BigInt(catOff), 64);
      recBuf.writeUInt32LE(catBuf.length, 72);
      recBuf.writeBigUInt64LE(BigInt(i), 76); // Contiguous vector row index

      recBuffers.push(recBuf);
    }

    // 2. Build Header
    const vectorTableOffset = HEADER_SIZE;
    const vectorTableSize = total * dim * 4;
    const recordTableOffset = vectorTableOffset + vectorTableSize;
    const recordTableSize = total * RECORD_SIZE;
    const stringTableOffset = recordTableOffset + recordTableSize;
    const stringTableSize = strBlob.length;

    const headerBuf = Buffer.alloc(HEADER_SIZE);
    headerBuf.write(HMB_MAGIC, 0, 8, "ascii");
    headerBuf.writeUInt32LE(HMB_VERSION, 8);
    headerBuf.writeUInt32LE(dim, 12);
    headerBuf.writeBigUInt64LE(BigInt(total), 16);
    headerBuf.writeBigUInt64LE(BigInt(vectorTableOffset), 24);
    headerBuf.writeBigUInt64LE(BigInt(recordTableOffset), 32);
    headerBuf.writeBigUInt64LE(BigInt(stringTableOffset), 40);
    headerBuf.writeBigUInt64LE(BigInt(stringTableSize), 48);
    headerBuf.writeBigUInt64LE(BigInt(this.createdAt), 56);
    headerBuf.writeBigUInt64LE(BigInt(this.lastSync), 64);
    // 64 bytes reserved are zeroed by Buffer.alloc

    // 3. Build Vector Matrix
    const vecBuf = Buffer.alloc(vectorTableSize);
    for (let i = 0; i < total; i++) {
      const v = this.memories[i].vector;
      const rowOffset = i * dim * 4;
      if (v && v.length === dim) {
        for (let d = 0; d < dim; d++) {
          vecBuf.writeFloatLE(v[d], rowOffset + d * 4);
        }
      } else {
        // Fallback: regenerate or zero fill
        const pad = generateSemanticEmbedding(this.memories[i].concept, dim);
        for (let d = 0; d < dim; d++) {
          vecBuf.writeFloatLE(pad[d], rowOffset + d * 4);
        }
      }
    }

    // 4. Contiguous Write: Single IO block to eliminate seeks
    const fullBinary = Buffer.concat([
      headerBuf,
      vecBuf,
      Buffer.concat(recBuffers),
      strBlob
    ]);

    const tmpPath = `${filepath}.tmp`;
    fs.writeFileSync(tmpPath, fullBinary);
    fs.renameSync(tmpPath, filepath);

    return {
      success: true,
      bytesWritten: fullBinary.length,
      anchors: total,
      filepath
    };
  }

  /**
   * Stores a new cognitive memory anchor.
   */
  async remember({
    concept,
    content,
    category = "EPISODIC",
    weight = 1.0,
    emotional_salience = 0.9,
    vector = null,
    vaultPath = null
  }) {
    await this.initialize();

    if (!concept) throw new Error("Missing required field 'concept'");
    if (!content) throw new Error("Missing required field 'content'");

    const now = getMicrosecondsNow();
    const cleanCat = String(category || "EPISODIC").toUpperCase().trim();
    const id = this.nextId++;

    let emb = vector;
    if (!emb || emb.length !== this.dim) {
      emb = generateSemanticEmbedding(`${concept} ${content} ${cleanCat}`, this.dim);
    } else if (!(emb instanceof Float32Array)) {
      emb = new Float32Array(emb);
    }

    const anchor = {
      id,
      domainHash: fnv1a64(cleanCat),
      weight: Math.max(0.0, Math.min(1.0, Number(weight) || 1.0)),
      emotional_salience: Math.max(0.0, Math.min(1.0, Number(emotional_salience) || 0.9)),
      timestamp: now,
      access_count: 1n,
      concept: String(concept).trim(),
      content: String(content).trim(),
      category: cleanCat,
      vector: emb
    };

    this.memories.push(anchor);
    this.saveToHmb(vaultPath || this.vaultPath);

    return {
      success: true,
      id: anchor.id.toString(),
      concept: anchor.concept,
      category: anchor.category,
      domainHash: "0x" + anchor.domainHash.toString(16),
      weight: anchor.weight,
      emotional_salience: anchor.emotional_salience,
      totalMemories: this.memories.length
    };
  }

  /**
   * Recalls top-K relevant memories using AVX-style vector cosine similarity + lexical matching.
   */
  async recall({
    query,
    category = null,
    topK = 5,
    minSimilarity = 0.1,
    vaultPath = null
  }) {
    await this.initialize();
    if (vaultPath && vaultPath !== this.vaultPath) {
      this.loadFromHmb(vaultPath);
    }

    if (!query) throw new Error("Missing required search parameter 'query'");

    const cleanQuery = String(query).trim().toLowerCase();
    const queryVec = generateSemanticEmbedding(cleanQuery, this.dim);
    const queryTokens = cleanQuery.split(/[\s,._\-:;!?/\\|()[\]{}<>"]+/).filter(t => t.length > 2);

    const scored = [];

    for (let i = 0; i < this.memories.length; i++) {
      const m = this.memories[i];

      // Category filter
      if (category && m.category.toUpperCase() !== category.toUpperCase()) {
        continue;
      }

      // Vector Cosine Similarity
      const vecSim = cosineSimilarity(queryVec, m.vector, this.dim);

      // Lexical Overlap Boost
      let lexicalHits = 0;
      const lowerConcept = m.concept.toLowerCase();
      const lowerContent = m.content.toLowerCase();
      for (const token of queryTokens) {
        if (lowerConcept.includes(token)) lexicalHits += 2.0;
        else if (lowerContent.includes(token)) lexicalHits += 1.0;
      }
      const lexicalScore = Math.min(0.5, (lexicalHits / Math.max(1, queryTokens.length)) * 0.35);

      // Haven attention equation:
      // total_score = (cosine_sim + lexical) * weight * (0.8 + 0.2 * emotional_salience)
      const combinedSim = Math.max(0.0, vecSim * 0.75 + lexicalScore);
      const salienceScale = 0.8 + 0.2 * m.emotional_salience;
      const finalScore = combinedSim * m.weight * salienceScale;

      if (finalScore >= minSimilarity || (lexicalHits > 0 && finalScore > 0.05)) {
        scored.push({
          index: i,
          score: finalScore,
          cosineSimilarity: vecSim,
          lexicalBoost: lexicalScore,
          memory: m
        });
      }
    }

    // Sort descending by score
    scored.sort((a, b) => b.score - a.score);

    const results = [];
    const limit = Math.min(scored.length, Number(topK) || 5);
    let updatedAccess = false;

    for (let i = 0; i < limit; i++) {
      const item = scored[i];
      // Increment access count
      this.memories[item.index].access_count++;
      updatedAccess = true;

      results.push({
        id: item.memory.id.toString(),
        score: Number(item.score.toFixed(4)),
        cosineSimilarity: Number(item.cosineSimilarity.toFixed(4)),
        concept: item.memory.concept,
        content: item.memory.content,
        category: item.memory.category,
        weight: item.memory.weight,
        emotional_salience: item.memory.emotional_salience,
        access_count: Number(item.memory.access_count),
        timestamp: new Date(Number(item.memory.timestamp / 1000n)).toISOString()
      });
    }

    if (updatedAccess) {
      // Background sync access counts to vault
      try {
        this.saveToHmb(vaultPath || this.vaultPath);
      } catch {}
    }

    return {
      query,
      resultsCount: results.length,
      totalVaultMemories: this.memories.length,
      memories: results
    };
  }

  /**
   * Returns memory stats, anchor counts, categories, and item previews.
   */
  async listMemories({ category = null, limit = 50, vaultPath = null } = {}) {
    await this.initialize();
    if (vaultPath && vaultPath !== this.vaultPath) {
      this.loadFromHmb(vaultPath);
    }

    const categories = {};
    for (const m of this.memories) {
      categories[m.category] = (categories[m.category] || 0) + 1;
    }

    let filtered = this.memories;
    if (category) {
      filtered = filtered.filter(m => m.category.toUpperCase() === category.toUpperCase());
    }

    // Sort by salience * weight descending
    filtered = [...filtered].sort((a, b) => {
      const scoreA = a.weight * (0.8 + 0.2 * a.emotional_salience);
      const scoreB = b.weight * (0.8 + 0.2 * b.emotional_salience);
      return scoreB - scoreA;
    });

    const items = filtered.slice(0, limit).map(m => ({
      id: m.id.toString(),
      concept: m.concept,
      content: m.content.length > 140 ? m.content.substring(0, 137) + "..." : m.content,
      category: m.category,
      weight: m.weight,
      emotional_salience: m.emotional_salience,
      access_count: Number(m.access_count),
      timestamp: new Date(Number(m.timestamp / 1000n)).toISOString()
    }));

    return {
      vaultPath: vaultPath || this.vaultPath,
      totalAnchors: this.memories.length,
      embeddingDim: this.dim,
      categories,
      displayedCount: items.length,
      memories: items
    };
  }

  /**
   * Bidirectional sync with haven-cpp's aura_vault.hmb
   */
  async syncWithHaven(havenVaultPath = HAVEN_CPP_VAULT_PATH) {
    await this.initialize();
    if (!fs.existsSync(havenVaultPath)) {
      throw new Error(`Haven vault does not exist at: ${havenVaultPath}`);
    }

    const tempEngine = new HmbEngine(havenVaultPath);
    tempEngine.loadFromHmb(havenVaultPath);

    let importedCount = 0;
    for (const remoteMem of tempEngine.memories) {
      const existing = this.memories.find(m => m.concept.toLowerCase() === remoteMem.concept.toLowerCase());
      if (!existing) {
        const id = this.nextId++;
        this.memories.push({
          ...remoteMem,
          id
        });
        importedCount++;
      }
    }

    this.saveToHmb(this.vaultPath);

    return {
      success: true,
      importedCount,
      totalVaultAnchors: this.memories.length,
      source: havenVaultPath
    };
  }

  /**
   * Exports memory bank to human-readable JSON format.
   */
  exportJson(jsonPath) {
    const data = {
      version: 2,
      engine: "gemini-super-system-hmb-64bit",
      totalAnchors: this.memories.length,
      embeddingDim: this.dim,
      memories: this.memories.map(m => ({
        id: m.id.toString(),
        concept: m.concept,
        content: m.content,
        category: m.category,
        weight: m.weight,
        emotional_salience: m.emotional_salience,
        access_count: Number(m.access_count),
        timestamp: Number(m.timestamp)
      }))
    };
    fs.writeFileSync(jsonPath, JSON.stringify(data, null, 2), "utf8");
    return { success: true, count: data.memories.length, jsonPath };
  }
}

// Singleton helper
let defaultEngine = null;
function getHmbEngine(vaultPath = DEFAULT_VAULT_PATH) {
  if (!defaultEngine || (vaultPath && defaultEngine.vaultPath !== vaultPath)) {
    defaultEngine = new HmbEngine(vaultPath);
  }
  return defaultEngine;
}

module.exports = {
  HmbEngine,
  getHmbEngine,
  fnv1a64,
  generateSemanticEmbedding,
  cosineSimilarity,
  DEFAULT_VAULT_PATH,
  HAVEN_CPP_VAULT_PATH
};
