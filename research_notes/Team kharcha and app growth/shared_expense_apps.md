# Shared / Group Expense Tracking Apps (India focus, as of Oct 2026)

Note: limited research pass (about 8 searches, no page fetches). Most facts come from search summaries. Treat the counts as indicative and check them before relying on them.

## Which apps do group/shared expenses well, and what are their core features?

### Takeaway
Splitwise is the main reference for splitting expenses, and Tricount and Settle Up are the low-friction alternatives built on links. Indian business apps (Vyapar, Khatabook) handle multiple users with staff roles, not with shared groups for logging spending. Google Pay's split feature is tied to payments and is weak for ongoing tracking. None of the apps found is a direct "team kharcha" (shared spending log, name-wise, daily/monthly) product for small shops.

### Cited Findings
- **Google Pay (India)**: bill split was announced at Google for India (Nov 2021) and launched Dec 2021. You create a group, add a bill, and request money from members — [BusinessToday 2021](https://www.businesstoday.in/technology/story/google-pay-gets-split-expense-feature-heres-how-it-works-316663-2021-12-23); [Google Pay Help](https://support.google.com/pay/india/answer/11420982?hl=EN)
- Google Pay groups hold up to 20 people — [Smartprix](https://us.smartprix.com/bytes/how-to-use-google-pay-to-split-bills-other-expenses)
- Reviewers (2021) said Google Pay is convenient for paying, but Splitwise is better at tracking several ongoing expenses over time — [XDA](https://www.xda-developers.com/google-pay-india-split-bill/); [BusinessToday](https://www.businesstoday.in/technology/story/google-pay-gets-split-expense-feature-heres-how-it-works-316663-2021-12-23)
- **Splitwise**: once integrated with Paytm in India for settling up (date not given) — [PBN](https://pbn.com/?p=158275)
- **Splitwise invites**: a group "Share a link" option opens the share sheet (WhatsApp, SMS and so on). A blog says members need a Splitwise account. No official QR join was found — [TapSmart](https://www.tapsmart.com/tips-and-tricks/shared-bills-splitwise/?amp=1); [robots.net](https://robots.net/fintech/how-to-join-a-group-in-splitwise/)
- **Tricount**: no account needed. The access link alone grants use. The app asks "who are you" only to compute your totals — [Tricount FAQ](https://www.tricount.com/en/do-i-need-a-login-or-account)
- Tricount links give full rights (add and delete) to anyone who has them, and they do not expire — [Tricount: who can access](https://tricount.com/en/who-can-access-my-tricount)
- Joining in the Tricount app: Add tricount > Join an existing tricount > paste link — [Tricount](https://tricount.com/en/how-to-share-tricount)
- **Settle Up**: works offline and syncs later. A group link lets non-users see balances in a browser without installing — [App Store listing](https://apps.apple.com/app/id737534985); [Settle Up tips](https://settleup.io/tips)
- **Vyapar**: Sync & Share lets the owner invite staff by phone or email with fixed roles (Secondary Admin, Salesman, Viewer, Biller, Accountant, Stock Keeper). The owner sees a per-user activity log and reports by user — [Vyapar blog](https://vyaparapp.in/blog/user-roles-permissions-vyapar/)
- Vyapar multi-device sync is a paid feature — [Vyapar guide](https://vyaparapp.in/guides/how-to-use-vyapar-app-on-multiple-devices)
- **Khatabook**: staff features are in a separate app, Pagarkhata (launched Dec 2020, salaries and attendance). The main app has general business expense tracking by category — [ap7am](https://www.ap7am.com/pn/3530/whatsapp:/send); [G2](https://www.g2.com/products/khatabook/discuss)
- Other apps named as Splitwise alternatives in India (2025, vendor-written list): tricount, Splid, Kittysplit, Settle Up, Google Pay — [Tricount blog](https://tricount.com/blog/top-splitwise-alternatives-in-india-2025-which-app-should-you-switch-to)
- Invite pattern in smaller apps: an 8-character invite code plus a link and QR, with the code valid for 48h (Olik Split). Owner and admins manage members (Let's Settle Up) — [Olik Split](https://oliksplit.app/features/group-sharing); [App Store](https://apps.apple.com/app/id6781202515)

### Inferences
- Two models exist: "split and settle" (Splitwise, Tricount, Google Pay) and "staff with roles logging into one business book" (Vyapar). Team kharcha fits the second, with the name-wise views from the first.
- Vyapar's per-user activity log and reports by user are the closest match to "see expenses name-wise".

### Gaps
- Walnut/axio, PhonePe and Paytm split features, OkCredit staff features, and Spendee/Wallet shared accounts were not researched (time budget).
- Monthly summary and PDF export details per app were not verified.

## What do users like and complain about (paywalls, privacy, offline)?

### Takeaway
The main complaint is Splitwise's daily cap on free expenses (3 or 4 per day, sources disagree, from about late 2023). It has pushed Indian users to alternatives. Tricount's open link is frictionless but has weak access control.

### Cited Findings
- Splitwise limits free users to a few expenses per day. One source says 3 per day; one quotes the help center as 4 per day. Pro removes the cap — [ITVoice](https://www.itvoice.in/splitwise-has-introduced-restrictions-on-the-number-of-free-expenses-users-can-add); [Finny (competitor)](https://getfinny.app/blog/splitwise-pricing-2026)
- Splitwise Pro costs about $4.99/month or $39.99/year. Receipt scanning and currency conversion are Pro-only, and free has ads (competitor source) — [Finny](https://getfinny.app/blog/splitwise-pricing-2026)
- Users ask for customizable roles in Vyapar because the predefined roles are limited — [Trustpilot](https://pt.trustpilot.com/review/vyaparapp.in)
- Tricount's link-only access means anyone with the link can delete expenses (privacy and trust risk) — [Tricount](https://tricount.com/en/who-can-access-my-tricount)

### Inferences
- A khata app with frequent small daily entries should not cap entries per day. That cap is the most resented paywall in this category.
- An audit trail showing who added or deleted what matters for trust in a shared log.

### Gaps
- No systematic Play Store or Reddit review analysis was done for the Indian apps.

## How are invites and joining handled with the least friction?

### Takeaway
Least friction: a shareable deep link (sent by WhatsApp through the share sheet) plus an optional short code/QR, with no account or login needed to view. The trade-off is security. Good practice is codes that expire, roles set by the admin, and identifying people by phone number.

### Cited Findings
- Tricount: link only, no login, never expires, full rights — [Tricount FAQ](https://www.tricount.com/en/do-i-need-a-login-or-account)
- Settle Up: browser view for non-users through a link — [App Store](https://apps.apple.com/app/id737534985)
- Splitwise: link through the share sheet, account required — [TapSmart](https://www.tapsmart.com/tips-and-tricks/shared-bills-splitwise/?amp=1)
- Vyapar: invite by phone or email with a role assigned — [Vyapar](https://vyaparapp.in/blog/user-roles-permissions-vyapar/)
- Olik Split: 8-character code encoded in a QR, valid for 48h — [Olik Split](https://oliksplit.app/features/group-sharing)

### Inferences
- For WhatsApp-heavy users, the recommended pattern is: admin taps "Invite" > WhatsApp message with a deep link and a 6–8 character code > joiner signs in by phone OTP > admin approves or assigns a role. Add a read-only web view for viewers who don't use the app.

### Gaps
- No data was found comparing conversion rates of different invite methods.

## Data on usage and adoption in India

### Takeaway
Only dated (2020–21) figures were found for Khatabook and OkCredit. No India user counts were found for Splitwise or Google Pay split.

### Cited Findings
- Khatabook: more than 8M merchants in 700+ districts, and over 10M MAU (2020) — [TechCrunch 2020](https://techcrunch.com/2020/05/19/indias-khatabook-raises-60-million-to-help-merchants-digitize-bookkeeping-and-accept-payments-online)
- Khatabook: "20M+ registered users, 11+ languages" (third-party listing, undated) — [Capterra](https://www.capterra.in/software/197475/khatabook)
- OkCredit: about 40M registrations, about 5.5M DAU, 2,800 cities (2021) — [YourStory 2021](https://yourstory.com/2021/12/okcredit-helping-small-businesses-maintain-credit-transactions/amp)
- OkCredit: 800M+ transactions recorded (Mar 2021) — [YourStory 2021](https://yourstory.com/2021/03/product-roadmap-fintech-startup-okcredit-800-million-transactions/amp)

### Gaps
- No 2025–26 figures were found for any of these apps. No Splitwise India downloads or Google Pay split usage data was found (it would need Sensor Tower/Appfigures).
