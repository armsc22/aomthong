(() => {
  const form = document.getElementById('cash-sale-form');
  if (!form) return;
  const body = document.getElementById('sale-lines');
  const template = body.firstElementChild.cloneNode(true);
  const money = value => value.toLocaleString('en-US', {minimumFractionDigits:2, maximumFractionDigits:2});
  function refresh() {
    let total = 0;
    Array.from(body.children).forEach((row, i) => {
      const select = row.querySelector('select');
      const quantity = row.querySelector('input');
      select.name = `Items[${i}].ProductId`; select.id = `sale-product-${i}`; select.setAttribute('aria-label', `สินค้าแถวที่ ${i + 1}`);
      quantity.name = `Items[${i}].Quantity`; quantity.id = `sale-quantity-${i}`; quantity.setAttribute('aria-label', `จำนวนแถวที่ ${i + 1}`);
      const price = Number(select.selectedOptions[0]?.dataset.price || 0);
      const amount = Math.round(price * 100) * Math.max(0, Number(quantity.value) || 0);
      total += amount;
      row.querySelector('.sale-line-total').textContent = money(amount / 100);
      row.querySelector('.sale-remove').disabled = body.children.length === 1;
    });
    document.getElementById('sale-total').textContent = money(total / 100);
  }
  body.addEventListener('input', refresh);
  body.addEventListener('change', refresh);
  body.addEventListener('click', event => {
    if (event.target.matches('.sale-remove') && body.children.length > 1) { event.target.closest('tr').remove(); refresh(); }
  });
  document.getElementById('sale-add').addEventListener('click', () => {
    if (body.children.length >= 50) return;
    const row = template.cloneNode(true); row.querySelector('select').value = ''; row.querySelector('input').value = '1'; body.appendChild(row); refresh();
  });
  form.addEventListener('submit', event => event.preventDefault());
  refresh();
})();
