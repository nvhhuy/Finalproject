import { create } from 'zustand'

export type ToastVariant = 'success' | 'error' | 'info' | 'warning'

export interface Toast {
  id: string
  message: string
  variant: ToastVariant
  durationMs: number
}

interface ToastState {
  toasts: Toast[]
  push: (toast: Omit<Toast, 'id'>) => string
  dismiss: (id: string) => void
}

let toastCounter = 0

function nextId() {
  toastCounter += 1
  return `toast-${toastCounter}`
}

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  push: (toast) => {
    const id = nextId()
    set((state) => ({ toasts: [...state.toasts, { ...toast, id }] }))
    return id
  },
  dismiss: (id) => {
    set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) }))
  },
}))
