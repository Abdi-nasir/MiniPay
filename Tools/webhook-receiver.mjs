import { createServer } from "node:http";
import { createHmac, timingSafeEqual } from "node:crypto";

const port = 4000;

const signingSecret =
  process.env.MINIPAY_WEBHOOK_SECRET ??
  "development-webhook-signing-secret-change-me";

function verifySignature({ timestamp, payload, signature }) {
  if (!timestamp || !signature) {
    return false;
  }

  const expected =
    "sha256=" +
    createHmac("sha256", signingSecret)
      .update(`${timestamp}.${payload}`)
      .digest("hex");

  const actualBuffer = Buffer.from(signature);
  const expectedBuffer = Buffer.from(expected);

  if (actualBuffer.length !== expectedBuffer.length) {
    return false;
  }

  return timingSafeEqual(actualBuffer, expectedBuffer);
}

const server = createServer((request, response) => {
  const chunks = [];

  request.on("data", chunk => {
    chunks.push(chunk);
  });

  request.on("end", () => {
    const payload = Buffer.concat(chunks).toString("utf8");

    const timestamp =
      request.headers["x-minipay-timestamp"];

    const signature =
      request.headers["x-minipay-signature"];

    const signatureValid = verifySignature({
      timestamp,
      payload,
      signature,
    });

    console.log({
      method: request.method,
      url: request.url,
      eventId:
        request.headers["x-minipay-event-id"],
      eventType:
        request.headers["x-minipay-event-type"],
      signatureValid,
      payload: JSON.parse(payload),
    });

    if (request.url?.includes("/fail")) {
      response.writeHead(500, {
        "Content-Type": "application/json",
      });

      response.end(JSON.stringify({
        received: false,
        reason: "Simulated merchant failure",
      }));

      return;
    }

    if (!signatureValid) {
      response.writeHead(401, {
        "Content-Type": "application/json",
      });

      response.end(JSON.stringify({
        received: false,
        reason: "Invalid signature",
      }));

      return;
    }

    response.writeHead(200, {
      "Content-Type": "application/json",
    });

    response.end(JSON.stringify({
      received: true,
    }));
  });
});

server.listen(port, "0.0.0.0", () => {
  console.log(
    `Webhook receiver listening on http://localhost:${port}`,
  );
});