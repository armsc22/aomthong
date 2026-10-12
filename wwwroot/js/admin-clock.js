(() => {
  const clock = document.getElementById("admin-clock");
  if (!clock) return;
  const date = document.getElementById("admin-clock-date");
  const formatter = new Intl.DateTimeFormat("th-TH-u-nu-latn", {
    timeZone: "Asia/Bangkok", hour: "2-digit", minute: "2-digit", second: "2-digit", hourCycle: "h23"
  });
  const dateFormatter = new Intl.DateTimeFormat("th-TH-u-ca-buddhist", {
    timeZone: "Asia/Bangkok", weekday: "long", day: "numeric", month: "long", year: "numeric"
  });
  function updateClock() {
    const now = new Date();
    clock.dateTime = now.toISOString();
    clock.textContent = formatter.format(now);
    if (date) date.textContent = dateFormatter.format(now);
  }
  updateClock();
  setInterval(updateClock, 1000);
})();
