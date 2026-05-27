import { THEME_STORAGE_KEY } from "@/lib/theme/theme";

/** Runs before paint to avoid light/dark flash (FOUC). */
export function ThemeScript() {
  const script = `
(function() {
  var key = ${JSON.stringify(THEME_STORAGE_KEY)};
  var stored = null;
  try { stored = localStorage.getItem(key); } catch (e) {}
  if (!stored) {
    var m = document.cookie.match(new RegExp('(?:^|; )' + key + '=([^;]*)'));
    stored = m ? m[1] : null;
  }
  var pref = stored === 'light' || stored === 'dark' || stored === 'auto' ? stored : 'auto';
  var dark = pref === 'dark' || (pref === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  var root = document.documentElement;
  root.classList.toggle('dark', dark);
  root.style.colorScheme = dark ? 'dark' : 'light';
})();
`;

  return <script dangerouslySetInnerHTML={{ __html: script }} />;
}
