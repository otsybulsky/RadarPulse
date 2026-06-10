import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitepress'

const siteBase = '/RadarPulse/'
const githubRepositoryUrl = 'https://github.com/otsybulsky/RadarPulse'
const githubRepositoryBranch = 'master'
const useGitHubRepoLinks = process.env.VITEPRESS_REPO_LINK_TARGET === 'github'
const configDir = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(configDir, '../..')
const docsRoot = path.join(repoRoot, 'docs')
const handbookRoot = path.join(docsRoot, 'handbook')
const localRepoRoute = `${siteBase}_repo/`
const allowedLocalRepoRoots = new Set(['README.md', 'docs', 'scripts', 'src', 'tests'])
const blockedLocalRepoSegments = new Set([
  '.angular',
  '.git',
  '.vitepress',
  'bin',
  'coverage',
  'dist',
  'node_modules',
  'obj',
  'test-results'
])

function normalizePath(value: string): string {
  return value.replace(/\\/g, '/')
}

function splitHref(href: string): { pathname: string; suffix: string } {
  const match = href.match(/^([^?#]*)(.*)$/)

  return {
    pathname: match?.[1] ?? href,
    suffix: match?.[2] ?? ''
  }
}

function isPublishedBookMarkdown(targetPath: string): boolean {
  const normalized = normalizePath(path.relative(handbookRoot, targetPath))

  return normalized === 'book-outline.md' || (normalized.startsWith('book/') && normalized.endsWith('.md'))
}

function encodeRepoPath(relativePath: string): string {
  return relativePath.split('/').map(encodeURIComponent).join('/')
}

function isBlockedLocalRepoPath(relativePath: string): boolean {
  return relativePath
    .split('/')
    .some(segment => blockedLocalRepoSegments.has(segment))
}

function isAllowedLocalRepoPath(targetPath: string): boolean {
  const relativePath = normalizePath(path.relative(repoRoot, targetPath))

  if (!relativePath || relativePath.startsWith('../') || path.isAbsolute(relativePath)) {
    return !relativePath
  }

  if (isBlockedLocalRepoPath(relativePath)) {
    return false
  }

  const root = relativePath.split('/')[0]

  return allowedLocalRepoRoots.has(root)
}

function localRepoUrlForPath(targetPath: string, suffix: string): string | null {
  if (!isAllowedLocalRepoPath(targetPath)) {
    return null
  }

  const relativePath = normalizePath(path.relative(repoRoot, targetPath))
  const directorySuffix = fs.existsSync(targetPath) && fs.statSync(targetPath).isDirectory()
    ? '/'
    : ''

  return `${localRepoRoute}${encodeRepoPath(relativePath)}${directorySuffix}${suffix}`
}

function githubRepoUrlForPath(targetPath: string, suffix: string): string | null {
  if (!isAllowedLocalRepoPath(targetPath)) {
    return null
  }

  const relativePath = normalizePath(path.relative(repoRoot, targetPath))

  if (!relativePath) {
    return `${githubRepositoryUrl}${suffix}`
  }

  const isDirectory = fs.existsSync(targetPath) && fs.statSync(targetPath).isDirectory()
  const view = isDirectory ? 'tree' : 'blob'
  const directorySuffix = isDirectory ? '/' : ''

  return `${githubRepositoryUrl}/${view}/${githubRepositoryBranch}/${encodeRepoPath(relativePath)}${directorySuffix}${suffix}`
}

function rewriteHrefForVitePress(href: string | null, currentFile: string | undefined): string | null {
  if (!href || !currentFile) {
    return href
  }

  if (/^(?:[a-z][a-z\d+\-.]*:|\/\/|#)/i.test(href)) {
    return href
  }

  const { pathname, suffix } = splitHref(href)
  const decodedPathname = decodeURIComponent(pathname)
  const currentDir = path.dirname(currentFile)
  let targetPath = path.resolve(currentDir, decodedPathname)

  if (!fs.existsSync(targetPath) && fs.existsSync(`${targetPath}.md`)) {
    targetPath = `${targetPath}.md`
  }

  if (!fs.existsSync(targetPath)) {
    return href
  }

  if (targetPath.endsWith('.md') && isPublishedBookMarkdown(targetPath)) {
    return href
  }

  return useGitHubRepoLinks
    ? githubRepoUrlForPath(targetPath, suffix) ?? href
    : localRepoUrlForPath(targetPath, suffix) ?? href
}

function resolveRepoRoutePath(routePath: string): string | null {
  const decodedRoutePath = decodeURIComponent(routePath)
  let targetPath = path.resolve(repoRoot, decodedRoutePath)

  if (!fs.existsSync(targetPath) && fs.existsSync(`${targetPath}.md`)) {
    targetPath = `${targetPath}.md`
  }

  if (!isAllowedLocalRepoPath(targetPath)) {
    return null
  }

  return targetPath
}

function contentTypeForPath(filePath: string): string {
  switch (path.extname(filePath).toLowerCase()) {
    case '.css':
      return 'text/css; charset=utf-8'
    case '.html':
      return 'text/plain; charset=utf-8'
    case '.js':
    case '.mjs':
    case '.ts':
    case '.tsx':
      return 'text/plain; charset=utf-8'
    case '.json':
      return 'application/json; charset=utf-8'
    case '.md':
    case '.cs':
    case '.csproj':
    case '.ps1':
    case '.sh':
    case '.txt':
    case '.yml':
    case '.yaml':
      return 'text/plain; charset=utf-8'
    case '.svg':
      return 'image/svg+xml'
    case '.png':
      return 'image/png'
    case '.jpg':
    case '.jpeg':
      return 'image/jpeg'
    case '.webp':
      return 'image/webp'
    default:
      return 'text/plain; charset=utf-8'
  }
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function directoryEntries(directoryPath: string): Array<{ name: string; href: string; isDirectory: boolean }> {
  return fs
    .readdirSync(directoryPath, { withFileTypes: true })
    .filter(entry => !entry.name.startsWith('.'))
    .map(entry => {
      const entryPath = path.join(directoryPath, entry.name)
      const relativePath = normalizePath(path.relative(repoRoot, entryPath))

      return {
        name: entry.name,
        href: `${localRepoRoute}${encodeRepoPath(relativePath)}${entry.isDirectory() ? '/' : ''}`,
        isDirectory: entry.isDirectory()
      }
    })
    .filter(entry => {
      const hrefPath = entry.href.slice(localRepoRoute.length).replace(/\/$/, '')
      const targetPath = path.resolve(repoRoot, decodeURIComponent(hrefPath))

      return isAllowedLocalRepoPath(targetPath)
    })
    .sort((left, right) => {
      if (left.isDirectory !== right.isDirectory) {
        return left.isDirectory ? -1 : 1
      }

      return left.name.localeCompare(right.name)
    })
}

function renderDirectoryListing(directoryPath: string): string {
  const relativePath = normalizePath(path.relative(repoRoot, directoryPath))
  const title = relativePath || 'Repository root'
  const entries = directoryEntries(directoryPath)
    .map(entry => {
      const marker = entry.isDirectory ? '/' : ''

      return `<li><a href="${entry.href}">${escapeHtml(entry.name)}${marker}</a></li>`
    })
    .join('\n')

  return `<!doctype html>
<html lang="uk">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${escapeHtml(title)}</title>
  <style>
    body { box-sizing: border-box; max-width: 960px; margin: 32px auto; padding: 0 20px; font: 16px/1.55 system-ui, sans-serif; }
    a { overflow-wrap: anywhere; }
    code { overflow-wrap: anywhere; }
  </style>
</head>
<body>
  <h1>${escapeHtml(title)}</h1>
  <p>Локальні файли репозиторію RadarPulse.</p>
  <ul>${entries}</ul>
</body>
</html>`
}

function renderTextFile(filePath: string, content: string): string {
  const relativePath = normalizePath(path.relative(repoRoot, filePath))

  return `<!doctype html>
<html lang="uk">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${escapeHtml(relativePath)}</title>
  <style>
    body { box-sizing: border-box; margin: 0; padding: 20px; font: 14px/1.55 ui-monospace, SFMono-Regular, Consolas, monospace; }
    h1 { font: 600 18px/1.4 system-ui, sans-serif; overflow-wrap: anywhere; }
    pre { white-space: pre-wrap; overflow-wrap: anywhere; word-break: break-word; }
  </style>
</head>
<body>
  <h1>${escapeHtml(relativePath)}</h1>
  <pre>${escapeHtml(content)}</pre>
</body>
</html>`
}

function writeDirectoryIndex(directoryPath: string, outDir: string): void {
  const relativePath = normalizePath(path.relative(repoRoot, directoryPath))
  const targetDir = path.join(outDir, '_repo', relativePath)
  const html = renderDirectoryListing(directoryPath)

  fs.mkdirSync(targetDir, { recursive: true })
  fs.writeFileSync(path.join(targetDir, 'index.html'), html, 'utf8')
  fs.writeFileSync(path.join(targetDir, 'index'), html, 'utf8')
}

function copyLocalRepoFile(filePath: string, outDir: string): void {
  const relativePath = normalizePath(path.relative(repoRoot, filePath))
  const targetPath = path.join(outDir, '_repo', relativePath)

  fs.mkdirSync(path.dirname(targetPath), { recursive: true })

  if (filePath.endsWith('.html')) {
    fs.writeFileSync(targetPath, renderTextFile(filePath, fs.readFileSync(filePath, 'utf8')), 'utf8')
  } else {
    fs.copyFileSync(filePath, targetPath)
  }

  if (filePath.endsWith('.md')) {
    fs.copyFileSync(filePath, path.join(outDir, '_repo', relativePath.slice(0, -'.md'.length)))
  }
}

function copyLocalRepoDirectory(directoryPath: string, outDir: string): void {
  writeDirectoryIndex(directoryPath, outDir)

  for (const entry of fs.readdirSync(directoryPath, { withFileTypes: true })) {
    const entryPath = path.join(directoryPath, entry.name)

    if (entry.name.startsWith('.') || !isAllowedLocalRepoPath(entryPath)) {
      continue
    }

    if (entry.isDirectory()) {
      copyLocalRepoDirectory(entryPath, outDir)
    } else {
      copyLocalRepoFile(entryPath, outDir)
    }
  }
}

function collectReferencedLocalRepoTargets(): string[] {
  const markdownFiles = [
    path.join(handbookRoot, 'book-outline.md'),
    ...fs
      .readdirSync(path.join(handbookRoot, 'book'))
      .filter(fileName => fileName.endsWith('.md'))
      .map(fileName => path.join(handbookRoot, 'book', fileName))
  ]
  const targets = new Set<string>()
  const markdownLinkPattern = /\[[^\]]+\]\(([^)]+)\)/g

  for (const markdownFile of markdownFiles) {
    const text = fs.readFileSync(markdownFile, 'utf8')
    let match: RegExpExecArray | null

    while ((match = markdownLinkPattern.exec(text))) {
      const href = match[1]

      if (/^(?:[a-z][a-z\d+\-.]*:|\/\/|#)/i.test(href)) {
        continue
      }

      const { pathname } = splitHref(href)
      let targetPath = path.resolve(path.dirname(markdownFile), decodeURIComponent(pathname))

      if (!fs.existsSync(targetPath) && fs.existsSync(`${targetPath}.md`)) {
        targetPath = `${targetPath}.md`
      }

      if (!fs.existsSync(targetPath) || isPublishedBookMarkdown(targetPath) || !isAllowedLocalRepoPath(targetPath)) {
        continue
      }

      targets.add(targetPath)
    }
  }

  return [...targets].sort()
}

function publishReferencedLocalRepoTargets(outDir: string): void {
  for (const targetPath of collectReferencedLocalRepoTargets()) {
    const stat = fs.statSync(targetPath)

    if (stat.isDirectory()) {
      copyLocalRepoDirectory(targetPath, outDir)
    } else {
      copyLocalRepoFile(targetPath, outDir)
    }
  }
}

export default defineConfig({
  title: 'Книга RadarPulse',
  description: 'Українська версія книги RadarPulse.',
  lang: 'uk-UA',
  base: siteBase,
  srcDir: 'handbook',
  ignoreDeadLinks: [
    /^\/_repo\//,
    /^\/RadarPulse\/_repo\//
  ],
  rewrites: {
    'book-outline.md': 'index.md'
  },
  srcExclude: [
    'README.md',
    'architecture.md',
    'glossary.md',
    'index.md',
    'processing-runtime.md',
    'product-surface.md',
    'source-map.md',
    'system-overview.md',
    'verification-and-evidence.md',
    'workflows.md'
  ],
  markdown: {
    config(md) {
      const defaultRender = md.renderer.rules.link_open ?? ((tokens, idx, options, env, self) => {
        return self.renderToken(tokens, idx, options)
      })

      md.renderer.rules.link_open = (tokens, idx, options, env, self) => {
        const token = tokens[idx]
        const rewrittenHref = rewriteHrefForVitePress(token.attrGet('href'), env?.realPath ?? env?.path)

        if (rewrittenHref) {
          token.attrSet('href', rewrittenHref)

          if (rewrittenHref.startsWith(localRepoRoute)) {
            token.attrSet('target', '_self')
          } else if (rewrittenHref.startsWith(githubRepositoryUrl)) {
            token.attrSet('target', '_blank')
            token.attrSet('rel', 'noreferrer')
          }
        }

        return defaultRender(tokens, idx, options, env, self)
      }
    }
  },
  vite: {
    plugins: [
      {
        name: 'radarpulse-local-repo-links',
        configureServer(server) {
          server.middlewares.use((req, res, next) => {
            const url = new URL(req.url ?? '/', 'http://localhost')

            if (!url.pathname.startsWith(localRepoRoute)) {
              next()

              return
            }

            const routePath = url.pathname.slice(localRepoRoute.length)
            const targetPath = resolveRepoRoutePath(routePath)

            if (!targetPath || !fs.existsSync(targetPath)) {
              res.statusCode = 404
              res.end('Not found')

              return
            }

            const stat = fs.statSync(targetPath)

            if (stat.isDirectory()) {
              res.setHeader('content-type', 'text/html; charset=utf-8')
              res.end(renderDirectoryListing(targetPath))

              return
            }

            res.setHeader('content-type', contentTypeForPath(targetPath))
            fs.createReadStream(targetPath).pipe(res)
          })
        },
        closeBundle() {
          if (!useGitHubRepoLinks) {
            publishReferencedLocalRepoTargets(path.join(configDir, 'dist'))
          }
        }
      }
    ]
  },
  themeConfig: {
    siteTitle: 'Книга RadarPulse',
    nav: [
      { text: 'План книги', link: '/' }
    ],
    sidebar: {
      '/': [
        {
          text: 'Книга',
          items: [
            { text: 'План книги', link: '/' },
            { text: 'Передмова', link: '/book/preface_executive_verdict' }
          ]
        },
        {
          text: 'Розділи',
          collapsed: false,
          items: [
            { text: '1. Двигун на верстаку', link: '/book/chapter_01_lab_table' },
            { text: '2. Бінарні лабіринти NEXRAD', link: '/book/chapter_02_nexrad_binaries' },
            { text: '3. Контракт радарного батча', link: '/book/chapter_03_radar_batch' },
            { text: '4. Монастир Домену', link: '/book/chapter_04_domain_monastery' },
            { text: '5. Архітектурні вартові', link: '/book/chapter_05_architecture_guards' },
            { text: '6. Контракти домену', link: '/book/chapter_06_domain_contracts' },
            { text: '7. Гарячий радар', link: '/book/chapter_07_hot_partitions' },
            { text: '8. Міграція топологій', link: '/book/chapter_08_topology_migration' },
            { text: '9. Анти-чорн патруль', link: '/book/chapter_09_anti_churn' },
            { text: '10. Асинхронний департамент', link: '/book/chapter_10_async_transport' },
            { text: '11. Алокаційна аномалія', link: '/book/chapter_11_allocation_anomaly' },
            { text: '12. Копіювання через пул (Pooled copy)', link: '/book/chapter_12_pooled_copy' },
            { text: '13. Холодний старт (Cold start)', link: '/book/chapter_13_cold_start' },
            { text: '14. Хаос паралельності (Concurrency chaos)', link: '/book/chapter_14_concurrency_chaos' },
            { text: '15. Впорядкований координатор (Ordered coordinator)', link: '/book/chapter_15_ordered_coordinator' },
            { text: '16. Мутабельне ядро', link: '/book/chapter_16_mutable_core' },
            { text: '17. Перерахунок топології (Stale recompute)', link: '/book/chapter_17_stale_recompute' },
            { text: '18. Стійкий конверт (Durable envelope)', link: '/book/chapter_18_durable_envelope' },
            { text: '19. Файлове стійке сховище', link: '/book/chapter_19_file_store' },
            { text: '20. Зупинка без неправди (Fail-closed)', link: '/book/chapter_20_fail_closed' },
            { text: '21. Користувацькі обробники', link: '/book/chapter_21_custom_handlers' },
            { text: '22. Контракт дельта/злиття', link: '/book/chapter_22_delta_merge' },
            { text: '23. Щит BFF (Backend-for-Frontend)', link: '/book/chapter_23_bff_shield' },
            { text: '24. Інтерфейс оператора', link: '/book/chapter_24_operator_ui' },
            { text: '25. Демо-пакет', link: '/book/chapter_25_demo_scripts' },
            { text: '26. Логування спостережуваності', link: '/book/chapter_26_observability_logging' }
          ]
        },
        {
          text: 'Додатки',
          collapsed: true,
          items: [
            { text: 'А. Профілювання', link: '/book/appendix_a_profiling' },
            { text: 'Б. Матриця доказів', link: '/book/appendix_b_claim_evidence_matrix' },
            { text: 'В. Production hardening (продукційне зміцнення)', link: '/book/appendix_c_production_hardening' },
            { text: 'Г. Reviewer attack pack (атаки рецензента)', link: '/book/appendix_d_reviewer_attack_pack' },
            { text: 'Д. Hostile reviewer transcript (ворожа рецензія)', link: '/book/appendix_e_simulated_hostile_reviewer_transcript' },
            { text: 'Е. Windows lab stand (лабораторний стенд)', link: '/book/appendix_f_lab_stand_bootstrap' },
            { text: 'Є. Linux lab stand (лабораторний стенд)', link: '/book/appendix_g_lab_stand_linux' }
          ]
        }
      ]
    },
    outline: {
      level: [2, 3]
    },
    footer: {
      message: 'Українська версія книги RadarPulse.',
      copyright: 'Опубліковано за допомогою VitePress і GitHub Pages.'
    }
  }
})
