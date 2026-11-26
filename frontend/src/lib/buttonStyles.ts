/**
 * How a button looks, apart from its size and place: `solid` for the main action (save, add), `outline` for the one
 * beside it (hold, minus, choose), `link` for small actions in text, `quiet` for minor ones (clear, cancel), `danger` for a link that undoes something. Screens
 * keep their own size and layout classes and add one of these.
 */
export const btn = {
  solid: 'bg-brand font-bold text-white shadow-md shadow-teal-900/20 transition hover:bg-brand-strong active:scale-[0.98] active:bg-brand-strong disabled:opacity-45 disabled:shadow-none',
  outline: 'border-[1.5px] border-brand bg-white font-bold text-brand-strong transition hover:bg-brand-soft active:scale-[0.98] disabled:opacity-45',
  /** Adds a row or starts something new: + Add item, + New order. Dashed so it never looks like Save. */
  add: 'inline-flex items-center justify-center gap-1.5 rounded-xl border-[1.5px] border-dashed border-brand-line bg-brand-soft/60 px-4 font-bold text-brand transition hover:bg-brand-soft active:scale-[0.98]',
  /** A button that cancels or removes; filled red only inside the confirm box. */
  dangerOutline: 'rounded-xl border-[1.5px] border-red-200 bg-white px-3 font-bold text-due transition hover:bg-due-soft active:scale-[0.98] disabled:opacity-45',
  link: 'font-semibold text-brand underline disabled:opacity-50',
  quiet: 'text-muted underline',
  /** A link that undoes or warns: reopen, remove, cancel an order. */
  danger: 'font-semibold text-due underline disabled:opacity-50',
}
