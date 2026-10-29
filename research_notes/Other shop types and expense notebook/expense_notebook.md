# Expense notebook (kharcha / rokad bahi) habits and app UX patterns for Indian kirana shops

Research date: 2026-10-29. Short run (about 8 tool calls; the connection dropped once). Many sources are vendor marketing or help pages, and none of the apps were tested hands-on.

## What shopkeepers write in their kharcha/rokad book (categories, daily tally, opening/closing balance)

### Takeaway
The rokad bahi is a daily cash book. Each line has date, particulars, money in, money out and a running balance. Each day opens with the previous day's closing cash and ends with a count of the cash box. The usual expenses are small and frequent: transport/loading, chai-nashta, packaging, repairs, supplier payments, staff advances and rent. UPI has broken the old "book = cash box" match.

### Cited Findings
- In most Indian shops this record already has a name, the "rokad bahi". It shows how much money moved today and how much should be in the box now — [Accountune](https://accountune.com/cash-book-format)
- Recommended shop format has 6 columns: Date, Particulars, In, Out, Mode (Cash/UPI/Card), Balance. The page says the textbook single/double/triple-column formats are not needed for a shop — [Accountune](https://accountune.com/cash-book-format)
- Typical expense particulars: tempo/loading charges, auto fare, courier; tea/snacks, packaging, stationery, string and tape; repairs paid from the drawer; supplier payments; staff advances; rent — [Accountune](https://accountune.com/cash-book-format)
- Closing cash = opening + cash received − cash paid. The worked example opens at ₹4,000 and closes at ₹8,370 — [Accountune](https://accountune.com/cash-book-format)
- Only Cash-mode lines change the cash balance. UPI and card lines are still recorded but go to the bank. Mode tags are used at month-end for bank reconciliation and GST — [Accountune](https://accountune.com/cash-book-format)
- Recommended habits: record each transaction as it happens and petty expenses immediately (not in a weekly catch-up); count the box every evening and match it to the book; if cash is short, trace that day's entries backwards — [Accountune](https://accountune.com/cash-book-format)
- Personal withdrawals are described as a "leak": cash taken from the box and never written down — [Accountune](https://accountune.com/cash-book-format)
- The source argues the all-cash rokad bahi no longer tallies for most shops, because UPI receipts go to the bank and not the drawer — [Accountune](https://accountune.com/cash-book-format) (2026 article)
- The bahi khata is a separate credit ledger, one page per customer — [Accountune](https://accountune.com/cash-book-format); [OkCredit](https://okcredit.in/bahi-khata-app)
- Textbook cash book variants: single column, double column (cash + bank), petty cash book — [Vedantu](https://www.vedantu.com/commerce/cash-book)
- Ezy Rokad markets itself as accounting software "based on handwritten cashbook". This shows demand for apps that copy the rokad layout — [Techjockey](https://techjockey.com/detail/ezyrokad)

### Inferences
- Starting category set for Grocery Khata: Maal kharid (wholesaler purchase), Kiraya (rent), Bijli (electricity), Staff/salary/advance, Bhada/transport/loading, Chai-pani/nashta, Packaging (thaili, string, tape), Repair, Bank deposit/withdrawal, Ghar kharcha/personal withdrawal, Other.
- Each expense needs a mode (Cash/UPI/Card/Bank), and only cash lines should change the drawer balance. This fits the existing cash book screen.
- An evening "count the box" step that shows the difference (short/excess) is a core notebook ritual and should be built in.

### Gaps
- No survey data was found on how often each category appears or how many shops keep a kharcha book at all. This needs field interviews.
- No authoritative source treats "personal withdrawal" or "bank" as a standard category; including them is an inference.

## How apps handle expense entry (Khatabook Cashbook, Vyapar, myBillBook, OkCredit, consumer trackers)

### Takeaway
Business apps (Vyapar, myBillBook) use a form: category, optional items, payment mode, optional GST with party/HSN, and a receipt image. Reports are grouped by category. Khatabook's Cashbook app is a simpler cash in/out book. Voice and natural-language entry (Hindi/Hinglish) is found mainly in newer AI consumer trackers, not in the big SME apps.

### Cited Findings
- Vyapar: open Expenses from the menu, see the category list and transactions, then tap Add Expense. A GST toggle at the top shows tax fields and party selection; with GST off, only a category is chosen — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-and-manage-expenses-in-vyapar-app)
- Vyapar: custom categories can be created and marked Direct or Indirect. This separates gross margin from net profit — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-and-manage-expenses-in-vyapar-app)
- Vyapar: expense items with price, tax rate and HSN/SAC fill GSTR-2 for ITC. State of supply sets CGST/SGST vs IGST. Without a vendor GSTIN, no ITC can be claimed — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-and-manage-expenses-in-vyapar-app)
- Vyapar: payment mode (cash/bank) on each entry; receipt via "Add Image"/"Add Document"; reports by expense, category and item, plus an Expense Category Report. The guide does not mention recurring expenses — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-and-manage-expenses-in-vyapar-app)
- myBillBook: "Expense Voucher" logs expenses by category. Recurring vendor payments (GST or non-GST) are named as a use case, but no auto-scheduling is described — [myBillBook help](https://knowledge.mybillbook.in/articles/8511360-how-to-create-an-expense-entry-in-mybillbook)
- Khatabook's Cashbook app: a free cash management and bookkeeping app for daily income, sales, expenses and credit/debit entries, with Sale & Expense reports. The listing looked stale (last release about 1,057 days before capture). Built-in categories were not confirmed — [Apptopia listing](https://apptopia.com/google-play/app/com.khatabook.cashbook/about)
- OkCredit's bahi khata app supports 11 languages including Hindi, Hinglish, Marathi, Tamil and Punjabi. It pitches digital records against paper registers that cannot be copied, searched, proved or recovered, and sends an SMS to the customer for each entry. Its page does not mention cash book or expense features — [OkCredit](https://okcredit.in/bahi-khata-app)
- AiXpense (Indian AI tracker): voice/chat entry in 22 Indian languages including Hinglish. One version uses Sarvam AI for accents and code-mixing. The free tier is limited to 7 entries per day — [Peerlist](https://peerlist.io/pratikjadhav/project/aixpense--ai-expense-tracker)
- Munshi: WhatsApp expense tracker where users type or send a voice note about spending — [Peerlist](https://peerlist.io/atybdot/project/munshi--whatsapp-expense-tracker)
- Money Vault / TalkieMoney: natural-language voice logging that lists Hindi among supported languages — [App Store](https://apps.apple.com/app/id6759955347); [App Store](https://apps.apple.com/us/app/talkiemoney-budget-ai-agent/id6473291180)

### Inferences
- Gap in the market: the large SME apps use forms; the AI apps that accept voice/Hinglish are consumer tools and not tied to a shop's cash book. Grocery Khata already uses Claude and Sarvam, so it can join the two.
- GST input on purchases is optional and should be off by default (most kirana expenses are non-GST petty cash). Keep a GSTIN/bill photo option for wholesaler purchases.

### Gaps
- Splitwise, Walnut/axio, Money Manager and Khatabook's in-app category lists were not researched (run cut short).
- No Play Store review analysis was done. Pain points such as "too many fields" are unverified.

## UX patterns that mimic a notebook (free text, date pages, running totals, Hinglish parsing, photos, voice, low literacy)

### Takeaway
The proven notebook pattern is one line per transaction with a running balance, grouped by day, with entries made as they happen. Voice/NL entry with code-mixed Hindi exists in AI trackers. No source found validates multi-item free-text parsing such as "chai 40, auto 120".

### Cited Findings
- Daily page model: date, particulars, in/out, mode, running balance, with opening carried from yesterday's closing — [Accountune](https://accountune.com/cash-book-format)
- "Record as it happens" is the habit to support, which means entry must be very fast — [Accountune](https://accountune.com/cash-book-format)
- Voice notes and typed free text as input (WhatsApp) — [Munshi](https://peerlist.io/atybdot/project/munshi--whatsapp-expense-tracker); Sarvam-based code-mixed voice parsing — [AiXpense](https://peerlist.io/pratikjadhav/project/aixpense--ai-expense-tracker)
- Receipt image attachment on expenses — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-and-manage-expenses-in-vyapar-app)
- Copy of a handwritten cashbook as a product approach — [Ezy Rokad](https://techjockey.com/detail/ezyrokad)

### Inferences (feature recommendation for Grocery Khata)
- **Notebook page view**: one screen per date ("aaj ka panna"), with lines shown as handwritten-style rows (particular, amount, mode chip), a sticky running total and a cash-in-hand figure. Swipe left/right moves between days.
- **One-line free-text box** at the bottom of the page: "chai 40, auto 120, thaili 250 upi". Split on commas/"aur"/new lines, take the number as the amount, map the word to a category by keyword (chai/nashta → Chai-pani; auto/tempo/bhada/rickshaw → Transport; bijli/light → Electricity; kiraya → Rent; pagaar/salary/advance + name → Staff; ghar/khud → Personal). A word "upi/online/phonepe/gpay" sets the mode; otherwise Cash. Handle Hindi numerals and words ("chalis", "sau", "dedh sau") using the existing Claude parser. Show parsed chips for one-tap confirm before saving.
- **Voice**: reuse the Sarvam STT and Claude pipeline already used for grocery lists. The mic sits next to the text box and its output fills the same line for confirmation.
- **Photo**: photograph a wholesaler bill or electricity bill. AI reads total, date and vendor (GSTIN optional) and suggests the "Maal kharid" category; the image is kept as a receipt.
- **Quick chips**: the shop's top 6 categories as big icon buttons with the last amount pre-filled (chai ₹40). Recurring items (rent, salary, electricity) offered as monthly reminders, not auto-posted.
- **Low literacy**: icons plus Hindi labels, large digits, a numeric keypad first, voice everywhere, no mandatory fields beyond amount, and read-back of the parsed line by TTS (optional).
- **Edit like a notebook**: a strike-through on delete (not a hard delete) keeps the audit trail and fits the paper habit.

### Gaps
- No UX case study or usability data was found on free-text multi-line expense entry for Indian low-literacy users. Recommend a pilot test.
- Accuracy of STT on numbers in Hinglish was not found. Needs testing with Sarvam.

## How expenses tie into profit reports, cash book reconciliation, and personal vs business separation

### Takeaway
Expenses feed two things: the cash book (only cash-mode lines change drawer cash; the evening count reconciles) and profit (direct vs indirect split gives gross versus net profit). Personal withdrawals are the main unrecorded leak, so they should be recorded but kept out of business expenses.

### Cited Findings
- Direct/indirect classification gives a view of gross margin versus net profit — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-and-manage-expenses-in-vyapar-app)
- Mode tagging drives month-end bank reconciliation; evening box count against book closing — [Accountune](https://accountune.com/cash-book-format)
- Personal cash taken from the box and not entered is a common cause of mismatch — [Accountune](https://accountune.com/cash-book-format)

### Inferences
- Treat "Ghar kharcha / owner withdrawal" as a drawings type: it lowers cash in hand but is excluded from the profit and loss report. "Maal kharid" (stock purchase) is cost of goods, not an operating expense; it could link to supplier khata if the shop bought on credit.
- Simple profit card: Sales − Maal kharid − other kharcha = Munafa, for today/week/month, with "personal" shown separately.
- Day close: expected cash (opening + cash sales + cash khata collections − cash expenses − withdrawals) against the counted cash, recording the difference as a "short/excess" line.

### Gaps
- No Indian SME survey data was found on how often owners mix personal and business cash.
- Khatabook/OkCredit profit-report behaviour for expenses was not verified.
