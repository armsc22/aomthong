// Quantity stepper on the product detail page.
document.querySelectorAll("[data-qty]").forEach((button) => {
  button.addEventListener("click", () => {
    const form = button.closest("form");
    const input = form?.querySelector("input[name='quantity']");
    if (!input) return;

    const min = Number(input.min || 1);
    const max = Number(input.max || 99);
    const delta = button.dataset.qty === "inc" ? 1 : -1;
    input.value = String(Math.min(max, Math.max(min, Number(input.value || min) + delta)));
  });
});
