import { describe, expect, it } from 'vitest'
import { parseDate, parseStatement, readNarration } from './statementImport'

describe('parseDate', () => {
  it('puts the day first and sets midnight in India', () => {
    expect(parseDate('07/11/2026')).toBe('2026-11-07T00:00:00+05:30')
    expect(parseDate('07-11-26')).toBe('2026-11-07T00:00:00+05:30')
    expect(parseDate('7 Nov 2026')).toBe('2026-11-07T00:00:00+05:30')
    expect(parseDate('2026-11-07 10:30')).toBe('2026-11-07T00:00:00+05:30')
  })

  it('is null for anything else', () => {
    expect(parseDate('Opening balance')).toBeNull()
    expect(parseDate('45/13/2026')).toBeNull()
  })
})

describe('readNarration', () => {
  it('reads the slash style', () => {
    expect(readNarration('UPI/CR/432112345678/RAMESH KUMAR/HDFC/ramesh@ok')).toEqual({ utr: '432112345678', payerName: 'RAMESH KUMAR' })
  })

  it('reads the dash style', () => {
    expect(readNarration('UPI-SURESH VERMA-suresh@okicici-ICIC0001234-432112345679-PAYMENT')).toEqual({ utr: '432112345679', payerName: 'SURESH VERMA' })
  })

  it('does not take a phone number in a UPI ID for the UTR', () => {
    expect(readNarration('UPI-RAMESH KUMAR-919876543210@ybl-HDFC0000001-432112345678-NOTE').utr).toBe('432112345678')
    expect(readNarration('UPI-RAMESH KUMAR-919876543210@ybl-HDFC0000001-NOTE').utr).toBeNull()
  })

  it('does not take an account number on a NEFT row for a UTR', () => {
    expect(readNarration('NEFT CR-HDFC0000001-SHARMA TRADERS-123456789012').utr).toBeNull()
  })

  it('leaves out what it cannot find', () => {
    expect(readNarration('NEFT CR 12345')).toEqual({ utr: null, payerName: null })
  })
})

describe('parseStatement', () => {
  const statement = [
    'Account statement for 123456',
    'Period,01/11/2026 to 07/11/2026',
    '',
    'Date,Narration,Chq./Ref.No.,Value Dt,Withdrawal Amt.,Deposit Amt.,Closing Balance',
    '05/11/2026,UPI/CR/432112345678/RAMESH KUMAR/HDFC/x,,05/11/26,,"1,200.50","10,000.00"',
    '05/11/2026,UPI/DR/432112345000/GROCER/HDFC/x,,05/11/26,500.00,,"9,500.00"',
    '06/11/2026,INTEREST PAID,,06/11/26,,45.00,"9,545.00"',
    'End of statement',
  ].join('\n')

  it('finds the table below the bank\'s own lines and keeps only money in by UPI or transfer', () => {
    const { rows, skipped, found } = parseStatement(statement)
    expect(found).toBe(true)
    expect(rows.map((r) => [r.amount, r.utr, r.payerName])).toEqual([[1200.5, '432112345678', 'RAMESH KUMAR']])
    expect(skipped).toBe(1)
  })

  it('reads one amount column with a Cr or Dr beside it', () => {
    const { rows } = parseStatement('Txn Date,Description,Amount,Dr/Cr\n05-11-2026,UPI-A KUMAR-a@ok-X-432112345670-N,300.00,CR\n05-11-2026,UPI-SHOP-s@ok-X-432112345671-N,50.00,DR')
    expect(rows.map((r) => r.amount)).toEqual([300])
  })

  it('does not take a Cr/Dr or Credit/Debit column for the amount', () => {
    expect(parseStatement('Date,Narration,Amount,Cr/Dr\n05/11/2026,UPI-A KUMAR-a@ok-X-432112345670-N,300.00,CR').rows.map((r) => r.amount)).toEqual([300])
    expect(parseStatement('Date,Narration,Amount,Credit/Debit\n05/11/2026,UPI-A KUMAR-a@ok-X-432112345670-N,300.00,Credit').rows.map((r) => r.amount)).toEqual([300])
  })

  it('says so when no table is there', () => {
    expect(parseStatement('just some text\nmore text').found).toBe(false)
  })
})
