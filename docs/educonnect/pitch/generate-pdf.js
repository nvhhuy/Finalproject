const { chromium } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();

  const htmlPath = path.resolve(__dirname, 'klassi-pitch-deck.html');
  await page.goto(`file://${htmlPath}`, { waitUntil: 'networkidle' });

  // Wait for fonts to load
  await page.waitForTimeout(2000);

  await page.pdf({
    path: path.resolve(__dirname, 'Klassi-Pitch-Deck-2026.pdf'),
    format: 'A4',
    landscape: true,
    margin: { top: 0, right: 0, bottom: 0, left: 0 },
    printBackground: true,
  });

  await browser.close();
  console.log('PDF generated: Klassi-Pitch-Deck-2026.pdf');
})();
