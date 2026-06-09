const http = require('http');
const fs = require('fs');
const path = require('path');
const puppeteer = require('puppeteer');
const { execSync } = require('child_process');

const distDir = path.resolve(__dirname, '../dist');
const mvcViewsDir = path.resolve(__dirname, '../../TotalParking/Views');
const mvcContentDir = path.resolve(__dirname, '../../TotalParking/Content');

// 1. Locate system browser (Edge or Chrome)
function getExecutablePath() {
  const paths = [
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe'
  ];
  for (const p of paths) {
    if (fs.existsSync(p)) return p;
  }
  return null;
}

// 2. Start temporary static file server to serve built dist/
const server = http.createServer((req, res) => {
  let urlPath = req.url === '/' ? '/index.html' : req.url;
  if (urlPath.startsWith('/SCADALayout/')) {
    urlPath = urlPath.substring('/SCADALayout/'.length - 1);
  }
  if (urlPath === '/') urlPath = '/index.html';
  
  const filePath = path.join(distDir, urlPath);
  const ext = path.extname(filePath);
  let contentType = 'text/html';
  if (ext === '.js') contentType = 'text/javascript';
  else if (ext === '.css') contentType = 'text/css';
  else if (ext === '.svg') contentType = 'image/svg+xml';
  else if (ext === '.png') contentType = 'image/png';
  
  fs.readFile(filePath, (err, content) => {
    if (err) {
      res.writeHead(404);
      res.end('Not Found');
    } else {
      res.writeHead(200, { 'Content-Type': contentType });
      res.end(content);
    }
  });
});

async function run() {
  console.log('Building React project for HTML extraction...');
  execSync('npm run build', { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' });

  server.listen(3000);
  console.log('Temporary static server running on port 3000.');

  const browserPath = getExecutablePath();
  if (!browserPath) {
    throw new Error('No system browser found (Edge/Chrome). Please make sure a browser is installed.');
  }
  console.log(`Using system browser at: ${browserPath}`);

  const browser = await puppeteer.launch({
    executablePath: browserPath,
    headless: true
  });

  const page = await browser.newPage();
  
  // Set default language state in localStorage
  await page.goto('http://localhost:3000/');
  await page.evaluate(() => {
    localStorage.setItem('scada_lang', 'vi');
  });
  await page.reload({ waitUntil: 'networkidle0' });

  // Extract Header HTML
  const headerHtml = await page.evaluate(() => {
    const el = document.querySelector('header');
    return el ? el.outerHTML : '';
  });

  // Find compiled CSS file in dist/assets
  const assetsDir = path.join(distDir, 'assets');
  const files = fs.readdirSync(assetsDir);
  const cssFile = files.find(f => f.startsWith('index-') && f.endsWith('.css'));
  
  if (!cssFile) {
    throw new Error('Could not find compiled CSS file in dist/assets.');
  }

  // Copy CSS to TotalParking/Content/scada.css
  if (!fs.existsSync(mvcContentDir)) {
    fs.mkdirSync(mvcContentDir, { recursive: true });
  }
  fs.copyFileSync(path.join(assetsDir, cssFile), path.join(mvcContentDir, 'scada.css'));
  console.log('Copied Tailwind compiled CSS to: TotalParking/Content/scada.css');

  // Build C# Razor Sidebar
  const sidebarRazor = `
@{
    var currentAction = ViewContext.RouteData.Values["action"]?.ToString().ToLower() ?? "index";
}
<aside class="w-14 lg:w-52 flex flex-col border-r border-[#334155] shrink-0" style="background: #1E293B">
  <nav class="flex-1 py-2 overflow-y-auto">
    <a href="@Url.Action("Index", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "index" ? "#0F172A" : "transparent"); border-left: @(currentAction == "index" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="layout-dashboard" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "index" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "index" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "index" ? "600" : "400")">Tổng quan</span>
      @if (currentAction == "index") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("FloorPlan", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "floorplan" ? "#0F172A" : "transparent"); border-left: @(currentAction == "floorplan" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="map" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "floorplan" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "floorplan" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "floorplan" ? "600" : "400")">Mặt bằng</span>
      @if (currentAction == "floorplan") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("Zones", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "zones" ? "#0F172A" : "transparent"); border-left: @(currentAction == "zones" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="grid-3x3" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "zones" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "zones" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "zones" ? "600" : "400")">Zone / Block</span>
      @if (currentAction == "zones") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("Routing", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "routing" ? "#0F172A" : "transparent"); border-left: @(currentAction == "routing" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="navigation" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "routing" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "routing" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "routing" ? "600" : "400")">Điều hướng xe</span>
      @if (currentAction == "routing") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("Alarms", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "alarms" ? "#0F172A" : "transparent"); border-left: @(currentAction == "alarms" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="bell-ring" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "alarms" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "alarms" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "alarms" ? "600" : "400")">Alarm & Event</span>
      @if (currentAction == "alarms") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("Maintenance", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "maintenance" ? "#0F172A" : "transparent"); border-left: @(currentAction == "maintenance" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="wrench" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "maintenance" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "maintenance" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "maintenance" ? "600" : "400")">Bảo trì</span>
      @if (currentAction == "maintenance") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("Reports", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "reports" ? "#0F172A" : "transparent"); border-left: @(currentAction == "reports" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="file-bar-chart-2" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "reports" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "reports" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "reports" ? "600" : "400")">Báo cáo</span>
      @if (currentAction == "reports") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("Cards", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "cards" ? "#0F172A" : "transparent"); border-left: @(currentAction == "cards" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="credit-card" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "cards" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "cards" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "cards" ? "600" : "400")">Quản lý thẻ</span>
      @if (currentAction == "cards") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("Settings", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "settings" ? "#0F172A" : "transparent"); border-left: @(currentAction == "settings" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="settings" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "settings" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "settings" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "settings" ? "600" : "400")">Cài đặt</span>
      @if (currentAction == "settings") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
    <a href="@Url.Action("Remote", "Home")" class="w-full flex items-center gap-3 px-3 py-2.5 mx-0 transition-colors hover:bg-[#0F172A] group" style="background: @(currentAction == "remote" ? "#0F172A" : "transparent"); border-left: @(currentAction == "remote" ? "3px solid #2563EB" : "3px solid transparent"); display: flex; text-decoration: none;">
      <i data-lucide="headphones" class="shrink-0 group-hover:text-[#60A5FA] transition-colors" style="color: @(currentAction == "remote" ? "#60A5FA" : "#94A3B8")"></i>
      <span class="hidden lg:block truncate" style="color: @(currentAction == "remote" ? "#F8FAFC" : "#94A3B8"); font-size: 13px; font-weight: @(currentAction == "remote" ? "600" : "400")">Remote Support</span>
      @if (currentAction == "remote") { <i data-lucide="chevron-right" class="hidden lg:block ml-auto shrink-0" style="color: #2563EB"></i> }
    </a>
  </nav>
  <div class="p-3 border-t border-[#334155] hidden lg:block">
    <div style="color: #475569; font-size: 10px; text-align: center">SCADA v2.4.1 | © TotalParking</div>
  </div>
</aside>
`;

  // Create layout HTML template
  const layoutHtml = `<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>TotalParking SCADA</title>
    <!-- Use extracted scada.css from C# Content folder -->
    <link rel="stylesheet" href="~/Content/scada.css" />
    <!-- Lucide CDN for static SVG icons integration -->
    <script src="https://unpkg.com/lucide@latest"></script>
    <style>
      html, body { height: 100%; margin: 0; }
      /* Customize scrollbars */
      ::-webkit-scrollbar { width: 6px; height: 6px; }
      ::-webkit-scrollbar-track { background: #1E293B; }
      ::-webkit-scrollbar-thumb { background: #475569; border-radius: 3px; }
      ::-webkit-scrollbar-thumb:hover { background: #64748B; }
    </style>
</head>
<body style="background: #0F172A; color: #F8FAFC; font-family: 'Inter', 'Segoe UI', sans-serif;">
    <div class="flex flex-col h-screen overflow-hidden">
        <!-- Header -->
        ${headerHtml}

        <div class="flex flex-1 overflow-hidden">
            <!-- Sidebar -->
            ${sidebarRazor}

            <!-- Main Content Area -->
            @RenderBody()
        </div>
    </div>

    <!-- Initialize Lucide Icons -->
    <script>
      lucide.createIcons();
    </script>
</body>
</html>
`;

  const layoutPath = path.join(mvcViewsDir, 'Shared/_ScadaLayout.cshtml');
  if (!fs.existsSync(path.dirname(layoutPath))) {
    fs.mkdirSync(path.dirname(layoutPath), { recursive: true });
  }
  fs.writeFileSync(layoutPath, layoutHtml);
  console.log('Shared layout Views/Shared/_ScadaLayout.cshtml created.');

  // 4. Scrape each screen and save as CSHTML view
  const screens = [
    { name: 'Index', path: '/' },
    { name: 'FloorPlan', path: '/floorplan' },
    { name: 'Zones', path: '/zones' },
    { name: 'Routing', path: '/routing' },
    { name: 'Alarms', path: '/alarms' },
    { name: 'Maintenance', path: '/maintenance' },
    { name: 'Reports', path: '/reports' },
    { name: 'Cards', path: '/cards' },
    { name: 'Settings', path: '/settings' },
    { name: 'Remote', path: '/remote' }
  ];

  const homeViewsDir = path.join(mvcViewsDir, 'Home');
  if (!fs.existsSync(homeViewsDir)) {
    fs.mkdirSync(homeViewsDir, { recursive: true });
  }

  for (const sc of screens) {
    console.log(`Extracting DOM for screen: ${sc.name} (${sc.path})...`);
    await page.goto(`http://localhost:3000${sc.path}`, { waitUntil: 'networkidle0' });

    // Extract inner content next to aside sidebar
    const mainContentHtml = await page.evaluate(() => {
      const sidebar = document.querySelector('aside');
      if (sidebar && sidebar.nextElementSibling) {
        return sidebar.nextElementSibling.outerHTML;
      }
      return '';
    });

    if (!mainContentHtml) {
      console.warn(`Warning: mainContentHtml is empty for ${sc.name}`);
      continue;
    }

    // Write child view
    const viewPath = path.join(homeViewsDir, `${sc.name}.cshtml`);
    const viewHtml = `@{
    Layout = "~/Views/Shared/_ScadaLayout.cshtml";
}

${mainContentHtml}
`;
    fs.writeFileSync(viewPath, viewHtml);
    console.log(`Created view: Views/Home/${sc.name}.cshtml`);
  }

  await browser.close();
  server.close();
  console.log('Conversion and export completed successfully!');
}

run().catch(err => {
  console.error('Error during DOM scraping and export:', err);
  server.close();
  process.exit(1);
});
