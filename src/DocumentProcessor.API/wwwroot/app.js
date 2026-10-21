const form = document.querySelector("#upload-form");
const fileInput = document.querySelector("#file");
const button = document.querySelector("#submit");
const statusNode = document.querySelector("#status");
const resultNode = document.querySelector("#result");
const maximumFileSize = 20 * 1024 * 1024;

form.addEventListener("submit", async (event) => {
  event.preventDefault();

  const file = fileInput.files[0];
  if (!file) return;
  if (file.size > maximumFileSize) {
    statusNode.textContent = "Maximum document size is 20 MB.";
    return;
  }

  button.disabled = true;
  statusNode.textContent = "Processing document…";
  resultNode.classList.add("hidden");

  try {
    const body = new FormData();
    body.append("file", file);
    const antiforgeryResponse = await fetch("/api/v1/documents/antiforgery");
    if (!antiforgeryResponse.ok) throw new Error("Could not prepare the secure upload.");
    const { requestToken } = await antiforgeryResponse.json();

    const response = await fetch("/api/v1/documents/process", {
      method: "POST",
      body,
      headers: { "X-CSRF-TOKEN": requestToken }
    });

    const payload = await response.json().catch(() => ({}));

    if (!response.ok) {
      if (payload.documentId) {
        statusNode.textContent = `Processing failed. Recorded document: ${payload.documentId}. ` +
          (payload.detail || payload.message || "");
        return;
      }
      throw new Error(
        payload.detail || payload.title || payload.message || "Processing failed."
      );
    }

    renderResult(payload);
    statusNode.textContent = "Processing completed.";
  } catch (error) {
    statusNode.textContent = error.message;
  } finally {
    button.disabled = false;
  }
});

function renderResult(processedDocument) {
  const analysis = processedDocument.analysis || {};
  const metadata = processedDocument.processingMetadata || {};

  document.querySelector("#document-type").textContent =
    formatEnum(analysis.documentType || "Unknown");

  const review = Boolean(analysis.requiresReview);
  const badge = document.querySelector("#review-badge");
  badge.textContent = review
    ? "Human review required"
    : "Arithmetic checks passed · verify source values";

  renderFields("#summary", [
    ["Supplier", analysis.supplierName],
    ["Supplier tax ID", analysis.supplierTaxId],
    ["Customer", analysis.customerName],
    ["Customer tax ID", analysis.customerTaxId],
    ["Document number", analysis.documentNumber],
    ["Issue date", analysis.issueDate],
    ["Due date", analysis.dueDate],
    ["Currency", analysis.currency],
    ["Subtotal", analysis.subtotal],
    ["Tax", analysis.taxTotal],
    ["Total", analysis.total],
    ["Header discount", formatAdjustment(analysis.headerDiscount, analysis.currency)],
    ["Line items", analysis.lines?.length ?? 0],
    ["Record ID", processedDocument.id]
  ]);

  renderLines(analysis.lines || [], analysis.currency);
  renderBreakdowns(analysis, analysis.currency);

  const issues = document.querySelector("#issues");
  issues.innerHTML = "";
  const values = analysis.issues || [];

  if (values.length === 0) {
    const item = document.createElement("li");
      item.textContent = "No arithmetic or completeness issues were detected. Verify all values against the original document.";
    issues.appendChild(item);
  } else {
    values.forEach(issue => {
      const item = document.createElement("li");
      const location = issue.lineIndex
        ? `Line ${issue.lineIndex}${issue.field ? ` · ${issue.field}` : ""}`
        : issue.field || "Document";
      item.textContent = `${formatEnum(issue.severity)} · ${location} · ${issue.message}`;
      issues.appendChild(item);
    });
  }

  renderFields("#telemetry", [
    ["Provider", metadata.provider],
    ["Model", metadata.model],
    ["Latency", metadata.latencyMs != null ? `${metadata.latencyMs} ms` : null],
    ["Input tokens", metadata.inputTokens],
    ["Cached input tokens", metadata.cachedInputTokens],
    ["Cache write tokens", metadata.cacheWriteTokens],
    ["Output tokens", metadata.outputTokens],
    ["Estimated cost",
      metadata.estimatedCostUsd != null ? `$${metadata.estimatedCostUsd}` : null]
  ]);

  resultNode.classList.remove("hidden");
}

function renderLines(lines, currency) {
  const target = document.querySelector("#line-items");
  target.replaceChildren();
  if (lines.length === 0) {
    target.textContent = "No line items were extracted.";
    return;
  }

  const table = document.createElement("table");
  const headings = ["Description", "Quantity", "Unit price", "Discount", "Tax rate", "Tax", "Net amount"];
  const head = document.createElement("thead");
  const headerRow = document.createElement("tr");
  headings.forEach(value => appendCell(headerRow, value, "th"));
  head.appendChild(headerRow);

  const body = document.createElement("tbody");
  lines.forEach(line => {
    const row = document.createElement("tr");
    [line.description, line.quantity, money(line.unitPrice, currency),
      formatAdjustment(line.discount, currency), percent(line.taxRate),
      money(line.taxAmount, currency), money(line.lineTotal, currency)]
      .forEach(value => appendCell(row, value ?? "—", "td"));
    body.appendChild(row);
  });

  table.append(head, body);
  target.appendChild(table);
}

function renderBreakdowns(analysis, currency) {
  const target = document.querySelector("#financial-breakdown");
  target.replaceChildren();
  const sections = [
    ["VAT", analysis.vatBreakdown],
    ["Surcharges", analysis.surcharges],
    ["Withholdings", analysis.withholdings]
  ];

  sections.forEach(([title, entries]) => {
    const block = document.createElement("div");
    block.className = "field";
    const heading = document.createElement("strong");
    heading.textContent = title;
    block.appendChild(heading);
    const list = document.createElement("ul");
    (entries || []).forEach(entry => {
      const item = document.createElement("li");
      item.textContent = `${percent(entry.rate) ?? "Rate unknown"} · base ${money(entry.base, currency)} · amount ${money(entry.amount, currency)}`;
      list.appendChild(item);
    });
    if (!entries?.length) {
      const item = document.createElement("li");
      item.textContent = "None extracted";
      list.appendChild(item);
    }
    block.appendChild(list);
    target.appendChild(block);
  });
}

function appendCell(row, value, elementName) {
  const cell = document.createElement(elementName);
  cell.textContent = value;
  row.appendChild(cell);
}

function money(value, currency) {
  if (value == null) return "—";
  return `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(value)} ${currency || ""}`.trim();
}

function percent(value) {
  return value == null ? null : `${value}%`;
}

function formatAdjustment(adjustment, currency) {
  if (!adjustment) return "—";
  const values = [percent(adjustment.rate), money(adjustment.amount, currency)]
    .filter(value => value != null && value !== "—");
  return values.length ? values.join(" · ") : "Present; values unclear";
}

function renderFields(selector, fields) {
  const target = document.querySelector(selector);
  target.innerHTML = "";

  fields.forEach(([label, value]) => {
    const wrapper = document.createElement("div");
    wrapper.className = "field";

    const caption = document.createElement("span");
    caption.textContent = label;

    const content = document.createElement("strong");
    content.textContent = value ?? "—";

    wrapper.append(caption, content);
    target.appendChild(wrapper);
  });
}

function formatEnum(value) {
  return String(value)
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
    .replace(/^./, char => char.toUpperCase());
}
