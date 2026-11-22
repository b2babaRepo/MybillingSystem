// Festive looks an owner can turn on for the online store: a coloured header and a greeting, nothing else changes.

export const FESTIVE: Record<string, { emoji: string; header: string }> = {
  diwali: { emoji: '🪔', header: 'bg-gradient-to-r from-amber-600 via-orange-600 to-rose-700' },
  holi: { emoji: '🎨', header: 'bg-gradient-to-r from-pink-500 via-purple-500 to-sky-500' },
  navratri: { emoji: '💃', header: 'bg-gradient-to-r from-red-600 via-orange-500 to-yellow-500' },
  eid: { emoji: '🌙', header: 'bg-gradient-to-r from-emerald-700 to-teal-600' },
  christmas: { emoji: '🎄', header: 'bg-gradient-to-r from-red-700 to-green-700' },
  independence: { emoji: '🇮🇳', header: 'bg-gradient-to-r from-orange-500 via-slate-500 to-green-600' },
  wedding: { emoji: '💍', header: 'bg-gradient-to-r from-rose-700 via-red-600 to-amber-600' },
  pongal: { emoji: '🌾', header: 'bg-gradient-to-r from-yellow-600 to-orange-600' },
}

export const festive = (theme: string | null | undefined) => (theme ? FESTIVE[theme] : undefined)
