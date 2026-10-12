(async () => {
  const status = document.getElementById('line-return-status');
  if (!status) return;
  // Read the destination before init: the SDK consumes the OAuth query itself.
  // Never copy OAuth codes/tokens into a manually constructed redirect URL.
  const requested = new URLSearchParams(window.location.search).get('liff.state');
  const destination = requested === '/Account/Profile' ? '/Account/Profile' : '/Account/Register';
  try {
    if (!status.dataset.liffId) throw new Error('Missing LIFF configuration');
    await liff.init({ liffId: status.dataset.liffId });
    // LIFF can perform its own secondary redirect during init. If still at
    // the endpoint after completion, continue only to our allowlisted routes.
    if (window.location.pathname === '/' || window.location.pathname === '/Home/Index') {
      if (!liff.isLoggedIn()) throw new Error('LINE login not completed');
      window.location.replace(destination);
    }
  } catch {
    status.textContent = 'เชื่อม LINE ยังไม่สำเร็จหรือการเข้าสู่ระบบหมดอายุ กรุณากลับไปเชื่อม LINE ใหม่';
    document.getElementById('line-return-retry').hidden = false;
  }
})();
