import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { DataTable } from './DataTable'

interface Due { id: string; name: string; balance: number }
const rows: Due[] = [
  { id: 'a', name: 'Ramesh Gupta', balance: 2600 },
  { id: 'b', name: 'Sunita Devi', balance: 1250 },
]
const columns = [
  { key: 'name', header: 'Customer', render: (r: Due) => r.name, sortValue: (r: Due) => r.name },
  { key: 'bal', header: 'Total due', align: 'right' as const, render: (r: Due) => `₹${r.balance}`, sortValue: (r: Due) => r.balance },
]

describe('DataTable', () => {
  it('renders a row per item, sortable headers and the total row', () => {
    const html = renderToStaticMarkup(
      <DataTable label="Dues" rows={rows} rowKey={(r) => r.id} columns={columns} footer={['Total', '₹3850']} selectedKey="b" />)
    expect(html.match(/<tr /g)?.length).toBe(4) // header, 2 rows, footer
    expect(html).toContain('Ramesh Gupta')
    expect(html).toContain('<tfoot>')
    expect(html).toContain('₹3850')
    expect(html.match(/<th[^>]*><button/g)?.length).toBe(2)
    expect(html).toContain('bg-brand-soft') // the chosen row
  })

  it('shows the phone cards and hides the table below desktop width when a card is given', () => {
    const html = renderToStaticMarkup(
      <DataTable rows={rows} rowKey={(r) => r.id} columns={columns} renderCard={(r) => <p>card {r.name}</p>} />)
    expect(html).toContain('lg:hidden')
    expect(html).toContain('hidden lg:block')
    expect(html).toContain('card Sunita Devi')
  })

  it('shows the empty message when there are no rows', () => {
    expect(renderToStaticMarkup(<DataTable rows={[]} rowKey={(r: Due) => r.id} columns={columns} empty={<p>none</p>} />)).toBe('<p>none</p>')
  })
})
