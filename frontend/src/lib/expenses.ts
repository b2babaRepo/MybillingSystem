import type { ExpenseCategory } from '../screens/ExpensesScreen'

/** Money out that is not a running cost, so it never lowers profit. The server keeps the same list. */
export const OUTSIDE_PROFIT: ExpenseCategory[] = ['purchase', 'home', 'tax', 'loan', 'equipment', 'deposit', 'advance']
