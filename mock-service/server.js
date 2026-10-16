const http = require("node:http");
const fs = require("node:fs");
const path = require("node:path");

const file = path.join(__dirname, "data", "customers.json");
fs.mkdirSync(path.dirname(file), { recursive: true });
if (!fs.existsSync(file)) fs.writeFileSync(file, "{}");

http.createServer((request, response) => {
  if (request.method === "GET" && request.url === "/customers") {
    response.setHeader("Content-Type", "application/json");
    return response.end(fs.readFileSync(file));
  }
  if ((request.method === "POST" && request.url === "/customers") ||
      (request.method === "PUT" && /^\/customers\/\d+$/.test(request.url))) {
    let body = "";
    request.on("data", chunk => body += chunk);
    request.on("end", () => {
      try {
        const customer = JSON.parse(body);
        const customers = JSON.parse(fs.readFileSync(file, "utf8"));
        customers[customer.customerId] = customer;
        fs.writeFileSync(file, JSON.stringify(customers, null, 2));
        response.writeHead(200, { "Content-Type": "application/json" });
        response.end(JSON.stringify({ ok: true }));
      } catch {
        response.writeHead(400);
        response.end();
      }
    });
    return;
  }
  response.writeHead(404);
  response.end();
}).listen(5051, () => console.log("Mock service listening on http://localhost:5051"));
