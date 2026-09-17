/**
 * Gemini Super System // Automatic MCP Schema Synchronizer
 * Keeps ~/.gemini/antigravity-cli/mcp/gemini-super/ tool schemas in exact parity with index.js
 */

const fs = require("fs");
const path = require("path");

function syncMcpSchemas(tools, customDir = null) {
  const targetDir = customDir || path.join(
    process.env.USERPROFILE || "C:\\Users\\admin",
    ".gemini",
    "antigravity-cli",
    "mcp",
    "gemini-super"
  );

  if (!fs.existsSync(targetDir)) {
    try {
      fs.mkdirSync(targetDir, { recursive: true });
    } catch {}
  }

  let updatedCount = 0;
  for (const tool of tools) {
    if (!tool || !tool.name) continue;
    const schema = {
      name: tool.name,
      description: tool.description,
      parameters: tool.inputSchema || { type: "object", properties: {} }
    };
    const schemaPath = path.join(targetDir, `${tool.name}.json`);
    const content = JSON.stringify(schema, null, 2);

    let needsWrite = true;
    if (fs.existsSync(schemaPath)) {
      try {
        const existing = fs.readFileSync(schemaPath, "utf8");
        if (existing === content) needsWrite = false;
      } catch {}
    }

    if (needsWrite) {
      try {
        fs.writeFileSync(schemaPath, content, "utf8");
        updatedCount++;
      } catch (err) {
        console.error(`[SchemaSync] Warning: Failed to write ${tool.name}.json:`, err.message);
      }
    }
  }

  return {
    targetDir,
    totalTools: tools.length,
    updatedCount
  };
}

module.exports = { syncMcpSchemas };
