const fs = require("fs");
const path = require("path");

// Read index.js and extract tools array
const indexPath = path.resolve(__dirname, "..", "index.js");
const indexContent = fs.readFileSync(indexPath, "utf8");

// Extract the tools array between SYSTEM_TOOLS / "tools: [" and the matching closing "]"
let startIdx = indexContent.indexOf("const SYSTEM_TOOLS = [");
let arrayPrefix = "const SYSTEM_TOOLS = ";
if (startIdx === -1) {
  startIdx = indexContent.indexOf("tools: [");
  arrayPrefix = "tools: ";
}
if (startIdx === -1) {
  console.error("Could not find SYSTEM_TOOLS or 'tools: [' in index.js");
  process.exit(1);
}

// Find the end of ListToolsRequestSchema
const endIdx = indexContent.indexOf("server.setRequestHandler(CallToolRequestSchema", startIdx);
const toolsCode = indexContent.substring(startIdx, endIdx);
const lastClosingBracket = toolsCode.lastIndexOf("]");
const cleanArrayCode = toolsCode.substring(arrayPrefix.length, lastClosingBracket + 1);

// Safely evaluate tools array in sandbox function
let tools = [];
try {
  tools = new Function(`return ${cleanArrayCode}`)();
} catch (e) {
  console.error("Failed to parse tools array:", e.message);
  process.exit(1);
}

const targetDir = path.join(process.env.USERPROFILE || "C:\\Users\\admin", ".gemini", "antigravity-cli", "mcp", "gemini-super");
if (!fs.existsSync(targetDir)) {
  fs.mkdirSync(targetDir, { recursive: true });
}

console.log(`Extracting ${tools.length} MCP tool schemas into: ${targetDir}`);

let createdCount = 0;
for (const tool of tools) {
  const schema = {
    name: tool.name,
    description: tool.description,
    parameters: tool.inputSchema || { type: "object", properties: {} }
  };
  const filePath = path.join(targetDir, `${tool.name}.json`);
  fs.writeFileSync(filePath, JSON.stringify(schema, null, 2), "utf8");
  console.log(`  ✓ ${tool.name}.json`);
  createdCount++;
}

console.log(`\nSuccessfully synced ${createdCount} MCP tool definitions!`);
