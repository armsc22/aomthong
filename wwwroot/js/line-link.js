(async () => {
  const button = document.getElementById('line-connect-btn');
  const status = document.getElementById('line-status');
  const form = document.getElementById('register-form');
  const submit = form?.querySelector('button[type="submit"]');
  const userId = document.getElementById('LineUserId');
  let verified = false;
  let pending;
  let submitting = false;
  const setVerified = value => {
    verified = value;
    if (submit) {
      submit.disabled = !value;
      submit.textContent = value ? 'สมัครสมาชิกและเปิดบัญชีออมทอง' : 'กรุณาเชื่อมบัญชี LINE ก่อนสมัคร';
    }
    if (!value && userId) userId.value = '';
  };
  setVerified(false);
  if (!button || !status) return;
  status.setAttribute('role', 'status');
  async function verify() {
    setVerified(false);
    button.disabled = true;
    status.textContent = 'กำลังตรวจสอบบัญชี LINE กรุณารอสักครู่…';
    try {
      const idToken = liff.getIDToken();
      if (!idToken) throw new Error('ไม่ได้รับหลักฐานจาก LINE กรุณาเข้าสู่ระบบ LINE ใหม่ และอนุญาตสิทธิ์ openid');
      const response = await fetch(button.dataset.bindUrl, {
        method: 'POST', credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', RequestVerificationToken: document.querySelector('input[name="__RequestVerificationToken"]').value },
        body: JSON.stringify({ idToken, nonce: document.getElementById('LinkNonce').value }),
        signal: AbortSignal.timeout(15000)
      });
      let result;
      try { result = await response.json(); } catch { result = null; }
      if (!response.ok) throw new Error(result?.error || (response.status === 429 ? 'ลองเชื่อม LINE หลายครั้งเกินไป กรุณารอหนึ่งนาที' : `ยืนยัน LINE ไม่สำเร็จ (${response.status}) กรุณาโหลดหน้าใหม่`));
      if (!result?.bound || !result.userId) throw new Error('เซิร์ฟเวอร์ยังไม่ยืนยันบัญชี LINE กรุณาลองเชื่อมใหม่');
      if (userId) userId.value = result.userId;
      const name = document.getElementById('LineDisplayName');
      if (name) name.value = result.displayName;
      setVerified(true);
      status.textContent = 'ยืนยัน LINE แล้ว: ' + result.displayName + ' — กรอกข้อมูลและสมัครได้';
      return true;
    } catch (error) {
      status.textContent = error.name === 'TimeoutError' ? 'การเชื่อม LINE ใช้เวลานาน กรุณากดเชื่อมอีกครั้ง' : error.message;
      return false;
    } finally { button.disabled = false; }
  }
  function bind() {
    if (!pending) pending = verify().finally(() => { pending = null; });
    return pending;
  }
  form?.addEventListener('submit', async event => {
    event.preventDefault();
    if (submitting) return;
    if (!verified) { status.textContent = 'กรุณากดเชื่อมบัญชี LINE ให้สำเร็จก่อนสมัคร'; button.focus(); return; }
    submitting = true;
    // Refresh proof immediately before posting; wait for the session write to finish.
    if (await bind()) HTMLFormElement.prototype.submit.call(form);
    else submitting = false;
  });
  if (!button.dataset.liffId) { button.disabled = true; status.textContent = 'ร้านยังไม่ได้ตั้งค่าเชื่อม LINE'; return; }
  button.disabled = true;
  status.textContent = 'กำลังเตรียมการเชื่อม LINE…';
  try {
    await liff.init({ liffId: button.dataset.liffId });
    button.disabled = false;
    button.addEventListener('click', () => {
      if (!liff.isLoggedIn()) liff.login({ redirectUri: window.location.origin + window.location.pathname });
      else bind();
    });
    if (liff.isLoggedIn()) await bind();
    else status.textContent = 'ขั้นตอนที่ 1: กด “เชื่อมบัญชี LINE” และเข้าสู่ระบบ LINE ก่อนกรอกข้อมูล';
  } catch {
    status.textContent = 'เริ่มต้น LINE ไม่สำเร็จ กรุณาเปิดผ่าน URL HTTPS ที่ตั้งใน LIFF แล้วโหลดหน้าใหม่';
  }
})();
