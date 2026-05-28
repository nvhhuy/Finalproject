import { lazy, type ComponentType } from 'react'

/** Lazy-load a route module that uses a named export (e.g. `export function UsersPage`). */
export function lazyNamedRoute<T extends ComponentType<unknown>>(
  loader: () => Promise<Record<string, T>>,
  exportName: string,
) {
  return lazy(() =>
    loader().then((module) => {
      const component = module[exportName]
      if (!component) {
        throw new Error(`lazyNamedRoute: export "${exportName}" not found`)
      }
      return { default: component }
    }),
  )
}
