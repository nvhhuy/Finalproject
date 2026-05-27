/** Browser confirm — message should already be translated. */
export function confirmAction(message: string): boolean {
  return window.confirm(message)
}
