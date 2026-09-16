# 🏛️ 64-Bit Haven Memory Bank (`.hmb`) Binary Specification

> **Specification for the contiguous binary cognitive memory bank format shared between `haven-cpp` and `gemini-super-system`.**

---

## 1. Design Principles

1. **Zero-Seek HDD Hardening**: Memory vaults are saved as a single contiguous sequential binary stream, eliminating head thrashing and seek delays on physical spinning hard drives.
2. **100% Bitwise Cross-Language Interoperability**: Bitwise identical across bare-metal C++20 (`haven-cpp`) and Node.js (`gemini-super-system`).
3. **In-Memory Contiguous Vector Matrix**: Flat matrix format optimized for AVX2 SIMD dot products and Float32Array vector math.
4. **Zero Third-Party Database Dependencies**: No Docker, no Pinecone, no Redis, no SQLite bloat.

---

## 2. Binary Layout

An `.hmb` file consists of four contiguous sections aligned to byte boundaries (`#pragma pack(push, 1)`):

```
+-------------------------------------------------------------+
| 1. Header (HmbHeader64) - Exactly 136 bytes                 |
+-------------------------------------------------------------+
| 2. Contiguous Float32 Vector Matrix                         |
|    Size: total_anchors * embedding_dim * 4 bytes            |
+-------------------------------------------------------------+
| 3. Record Metadata Table (HmbRecord64[])                    |
|    Size: total_anchors * 84 bytes                           |
+-------------------------------------------------------------+
| 4. UTF-8 Contiguous String Table Blob                       |
|    Size: string_table_size bytes                            |
+-------------------------------------------------------------+
```

---

## 3. Data Structures

### `HmbHeader64` (136 Bytes)

| Offset | Type | Field | Description |
|---|---|---|---|
| `0x00` | `char[8]` | `magic` | ASCII `"HAVENMEM"` (`0x484156454E4D454D`) |
| `0x08` | `uint32` | `version` | `0x00020000` (v2.0 64-bit) |
| `0x0C` | `uint32` | `embedding_dim` | Latent vector dimensions (default: `128`) |
| `0x10` | `uint64` | `total_anchors` | Total count of memory records in vault |
| `0x18` | `uint64` | `vector_table_offset` | Byte offset to contiguous vector array (`136`) |
| `0x20` | `uint64` | `record_table_offset` | Byte offset to record table |
| `0x28` | `uint64` | `string_table_offset` | Byte offset to string blob |
| `0x30` | `uint64` | `string_table_size` | Length of string blob in bytes |
| `0x38` | `uint64` | `created_at` | Microsecond Unix epoch timestamp |
| `0x40` | `uint64` | `last_sync` | Microsecond Unix epoch timestamp |
| `0x48` | `uint8[64]`| `reserved` | 64-byte expansion space (zero-filled) |

### `HmbRecord64` (84 Bytes)

| Offset | Type | Field | Description |
|---|---|---|---|
| `0x00` | `uint64` | `memory_id` | Unique 64-bit memory identifier |
| `0x08` | `uint64` | `domain_hash` | 64-bit FNV-1a hash of category domain |
| `0x10` | `float32` | `weight` | Importance / salience score (`0.0` to `1.0`) |
| `0x14` | `float32` | `emotional_salience` | Affective resonance score (`0.0` to `1.0`) |
| `0x18` | `int64` | `timestamp` | Microsecond Unix epoch timestamp |
| `0x20` | `uint64` | `access_count` | Recall frequency counter |
| `0x28` | `uint64` | `concept_offset` | Byte offset in string table |
| `0x30` | `uint32` | `concept_len` | Byte length of concept string |
| `0x34` | `uint64` | `content_offset` | Byte offset in string table |
| `0x3C` | `uint32` | `content_len` | Byte length of content string |
| `0x40` | `uint64` | `category_offset` | Byte offset in string table |
| `0x48` | `uint32` | `category_len` | Byte length of category string |
| `0x4C` | `uint64` | `vector_idx` | Row index in contiguous vector table |

---

## 4. Hash Function (64-Bit FNV-1a)

Category domain strings are hashed using standard 64-bit FNV-1a:
* **Offset Basis**: `14695981039346656037ULL` (`0xCBF29CE484222325`)
* **Prime**: `1099511628211ULL` (`0x100000001B3`)

```javascript
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
```

---

## 5. Scoring & Retrieval Equation

Memory relevance combines dense vector cosine similarity and lexical keyword overlap, scaled by affective salience:

$$\text{Sim}_{\text{combined}} = \max\left(0, \text{Sim}_{\text{cosine}} \times 0.75 + \text{Score}_{\text{lexical}}\right)$$
$$\text{Score}_{\text{final}} = \text{Sim}_{\text{combined}} \times \text{Weight} \times \left(0.8 + 0.2 \times \text{EmotionalSalience}\right)$$

When an anchor is recalled, its `access_count` increments automatically, prioritizing frequently consulted knowledge over time.
