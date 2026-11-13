// Finds and downloads a CC0 / public-domain photograph from Wikimedia Commons, recording license metadata.
// Usage: node tools/fetch-cc0-photo.mjs "<search terms>" <out.jpg>
import fs from "node:fs";
import path from "node:path";

const [query, out] = process.argv.slice(2);
const api = new URL("https://commons.wikimedia.org/w/api.php");
api.search = new URLSearchParams({
  action: "query", format: "json", generator: "search", gsrnamespace: "6", gsrlimit: "40",
  gsrsearch: `${query} filetype:bitmap`, prop: "imageinfo", iiprop: "url|size|extmetadata|mime", iiurlwidth: "1600"
}).toString();
const ua = { "user-agent": "CursivisSpatialFixtures/1.0 (hackathon test fixtures; contact via github.com/UnknownGod2011)" };
const json = await (await fetch(api, { headers: ua })).json();
const pages = Object.values(json?.query?.pages || {});
const candidates = pages
  .map((p) => ({ title: p.title, info: p.imageinfo?.[0] }))
  .filter((c) => c.info && /jpeg|png/.test(c.info.mime))
  .map((c) => ({ ...c, license: c.info.extmetadata?.LicenseShortName?.value || "", artist: (c.info.extmetadata?.Artist?.value || "").replace(/<[^>]+>/g, "").trim() }))
  .filter((c) => /^(CC0|Public domain|PD)/i.test(c.license))
  .filter((c) => c.info.width >= 1200 && c.info.width > c.info.height);
if (!candidates.length) {
  console.log("no CC0/PD candidates");
  process.exit(1);
}
const pick = candidates[0];
const img = await fetch(pick.info.thumburl || pick.info.url, { headers: ua });
const buf = Buffer.from(await img.arrayBuffer());
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, buf);
const credit = { file: path.basename(out), title: pick.title, source: pick.info.descriptionurl, license: pick.license, artist: pick.artist };
fs.writeFileSync(`${out}.credit.json`, JSON.stringify(credit, null, 2));
console.log(JSON.stringify({ ...credit, bytes: buf.length }));
