// Back-office requests use the administrator cookie and an anti-forgery token.
const $ = (selector) => document.querySelector(selector);
const money = (value) => Number(String(value).replace(/,/g, ""));
const fmt = (value) => Number(value).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const thaiDate = (iso) => {
  const [y, m, d] = iso.split("-").map(Number);
  return `${String(d).padStart(2, "0")}/${String(m).padStart(2, "0")}/${y + 543}`;
};
let selectedCustomerId = Number($("#customer-id")?.value) || null;
let currentBalance = money($("#current").value);
let busy = false;
const setStatus = (text, state) => {
  $("#core-status").textContent = text;
  $("#core-status").classList.remove("connected", "error");
  if (state) $("#core-status").classList.add(state);
};
const setBusy = (value) => {
  busy = value;
  document.querySelectorAll(".lookup, #add-transaction, #account, #citizen").forEach(element => element.disabled = value);
};
async function request(path, options) {
  const response = await fetch(path, options);
  if (!response.ok) throw new Error(response.status === 401 ? "กรุณาเข้าสู่ระบบผู้ดูแลอีกครั้ง" : "ทำรายการไม่สำเร็จ กรุณาตรวจสอบข้อมูล");
  return response.json();
}
function preview() {
  const amount = money($("#transaction-amount").value) || 0;
  const withdraw = $('input[name="operation"]:checked')?.value === "2";
  $("#total").value = fmt(amount);
  $("#current").value = fmt(currentBalance + (withdraw ? -amount : amount));
}
$("#transaction-amount").addEventListener("input", preview);
document.querySelectorAll('input[name="operation"]').forEach(input => input.addEventListener("change", preview));
request("/api/core/status").then(() => setStatus("Core DB เชื่อมต่อแล้ว", "connected")).catch(error => setStatus(error.message, "error"));

// Editing a lookup field invalidates the old selection until lookup succeeds.
for (const selector of ["#account", "#citizen"]) {
  $(selector).addEventListener("input", () => { selectedCustomerId = null; });
}
document.querySelectorAll(".lookup").forEach(button => {
  button.addEventListener("click", async () => {
    if (busy) return;
    const query = button.parentElement.querySelector("input").value.trim();
    selectedCustomerId = null;
    if (!query) { setStatus("กรุณาระบุข้อมูลค้นหา", "error"); return; }
    setBusy(true);
    try {
      const customers = await request(`/api/core/customers/search?q=${encodeURIComponent(query)}`);
      if (customers.length !== 1) throw new Error(customers.length ? "พบหลายบัญชี กรุณาระบุข้อมูลให้ครบถ้วน" : "ไม่พบสมาชิก");
      const customer = customers[0];
      const summary = await request(`/api/core/deposits/${customer.id}`);
      if (!customer.account) throw new Error("ไม่พบบัญชีเงินฝาก");
      $("#customer-id").value = String(customer.id);
      $("#member").value = customer.memberCode;
      $("#full-name").value = $("#account-name").value = `${customer.firstName} ${customer.lastName}`.trim();
      $("#citizen").value = customer.citizenId;
      $("#account").value = customer.account.accountNumber;
      $("#interest").value = fmt(customer.account.interestRate);
      $("#deposit-type").value = customer.account.depositTypeCode;
      $("#deposit-type-name").value = customer.account.depositTypeName;
      $("#branch").value = customer.account.branch;
      $("#transaction-amount").value = "0.00";
      applySummary(summary);
      selectedCustomerId = customer.id;
      setStatus("โหลดข้อมูลจาก Core DB แล้ว", "connected");
    } catch (error) { setStatus(error.message, "error"); }
    finally { setBusy(false); }
  });
});
$("#add-transaction").addEventListener("click", async () => {
  if (busy) return;
  if (!selectedCustomerId) { setStatus("กรุณาค้นหาสมาชิกก่อนลงสมุด", "error"); return; }
  const amount = money($("#transaction-amount").value);
  const operation = $('input[name="operation"]:checked')?.value;
  if (!Number.isFinite(amount) || amount <= 0 || amount > 1000000000 || !["1", "2"].includes(operation)) {
    setStatus("กรุณาระบุยอดเงินและประเภทรายการให้ถูกต้อง", "error"); return;
  }
  setBusy(true);
  try {
    const summary = await request(`/api/core/deposits/${selectedCustomerId}/${operation === "2" ? "withdraw" : "deposit"}`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').value },
      body: JSON.stringify({ amount })
    });
    $("#transaction-amount").value = "0.00";
    applySummary(summary);
    setStatus("ลงสมุดเรียบร้อย", "connected");
  } catch (error) {
    selectedCustomerId = null;
    setStatus(error.message + " กรุณาค้นหาบัญชีเพื่อตรวจสอบยอดก่อนทำรายการอีกครั้ง", "error");
  } finally { setBusy(false); }
});
function applySummary(summary) {
  currentBalance = summary.totalBalance;
  $("#balance").value = fmt(summary.principalBalance);
  for (const [id, key] of [["deposit", "totalDeposit"], ["withdraw", "totalWithdrawal"], ["interest", "totalInterest"], ["balance", "totalBalance"]]) {
    $(`#sum-${id}`).textContent = fmt(summary[key]);
  }
  const rate = Number($("#daily-dividend").dataset.annualRate);
  $("#daily-dividend").value = (currentBalance * rate / 100 / 365).toFixed(4);
  const ledger = $("#ledger");
  ledger.replaceChildren();
  summary.entries.forEach(entry => {
    const row = document.createElement("tr");
    for (const value of [entry.seq, thaiDate(entry.date), fmt(entry.deposit), fmt(entry.withdrawal), fmt(entry.interest), fmt(entry.balanceAfter), entry.status]) {
      const cell = document.createElement("td"); cell.textContent = value; row.appendChild(cell);
    }
    const cell = document.createElement("td");
    const check = document.createElement("input"); check.type = "checkbox"; check.className = "check";
    cell.appendChild(check); row.appendChild(cell); ledger.appendChild(row);
  });
  ledger.lastElementChild?.classList.add("selected");
  $("#transaction").value = `DW${String(summary.entries.length + 1).padStart(6, "0")}`;
  preview();
}
$("#identity-button")?.addEventListener("click", () => setStatus("ยังไม่ได้เชื่อมต่อเครื่องอ่านบัตรประชาชน", "error"));
