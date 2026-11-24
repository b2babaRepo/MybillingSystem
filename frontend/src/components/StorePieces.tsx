import { useTranslation } from 'react-i18next'
import { formatRupees, whatsAppLink } from '../lib/format'

/** A tagged piece in a jewellery shop, priced at today's rate (GST included), as the shop's online page shows it. */
export interface StorePiece { tagNo: number; name: string; category: string | null; metal: string; purity: number; karat: string; gross: number; net: number; huid: string | null; price: number }

/** The shop's tagged pieces in stock with today's price, each with a WhatsApp question to the shop. */
export function StorePieces({ pieces, phone }: { pieces: StorePiece[]; phone: string | null }) {
  const { t } = useTranslation()
  if (pieces.length === 0) return null
  return (
    <section aria-label={t('jwl5.piecesTitle')} className="mx-4 mt-3 flex flex-col gap-2">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="text-[17px] font-extrabold">{t('jwl5.piecesTitle')}</h2>
        <span className="text-xs text-muted">{t('jwl5.piecesCount', { count: pieces.length })}</span>
      </div>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
        {pieces.map((p) => (
          <article key={p.tagNo} className="flex flex-col gap-1 rounded-2xl border border-amber-200 bg-white p-3">
            <span className="flex items-center justify-between gap-1 text-xs font-bold text-amber-800">
              <span>{p.metal === 'gold' ? p.karat : `${t('cust5.silver')} ${p.purity}`}</span>
              {p.huid && <span className="rounded-full bg-[#e3f2ee] px-2 py-0.5 text-[10px] text-[#0b5e4f]">HUID</span>}
            </span>
            <b className="line-clamp-2 text-[15px] leading-tight">{p.name}</b>
            <span className="text-xs text-muted">{p.net.toFixed(3)} g{p.category ? ` · ${p.category}` : ''}</span>
            <b className="mt-auto text-lg tabular-nums">{formatRupees(p.price)}</b>
            {phone && (
              <a href={whatsAppLink(phone, t('jwl5.askMsg', { name: p.name, tag: p.tagNo, price: formatRupees(p.price) }))} target="_blank" rel="noreferrer"
                className="flex min-h-11 items-center justify-center rounded-xl bg-[#1f9d55] px-2 text-sm font-bold text-white">{t('jwl5.askPiece')}</a>
            )}
          </article>
        ))}
      </div>
      <p className="text-[11px] text-muted">{t('jwl5.piecesNote')}</p>
    </section>
  )
}
