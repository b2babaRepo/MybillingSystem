// Stroke icons from the "Clean Teal" mockup. Decorative: the text beside each one says what it does.

type IconProps = { size?: number; className?: string }

function Svg({ size = 24, className, children }: IconProps & { children: React.ReactNode }) {
  return (
    <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" className={className}>
      {children}
    </svg>
  )
}

export const HomeIcon = (p: IconProps) => <Svg {...p}><path d="M3 11 12 3l9 8v10H3z" /></Svg>

export const KhataIcon = (p: IconProps) => <Svg {...p}><path d="M4 4h12a4 4 0 0 1 4 4v12H8a4 4 0 0 1-4-4z" /><path d="M8 9h8M8 13h6" /></Svg>

export const BillIcon = (p: IconProps) => <Svg {...p}><path d="M6 2h12v20l-3-2-3 2-3-2-3 2z" /><path d="M9 7h6M9 11h6M9 15h4" /></Svg>

export const ItemsIcon = (p: IconProps) => <Svg {...p}><path d="M20 7H9M14 17H4" /><circle cx="6" cy="7" r="2" /><circle cx="18" cy="17" r="2" /></Svg>

export const PlusIcon = (p: IconProps) => <Svg {...p}><path d="M12 5v14M5 12h14" /></Svg>

export const PaymentIcon = (p: IconProps) => <Svg {...p}><rect x="3" y="6" width="18" height="12" rx="2" /><circle cx="12" cy="12" r="2.5" /></Svg>

export const CustomersIcon = (p: IconProps) => <Svg {...p}><circle cx="9" cy="8" r="4" /><path d="M2 21c0-4 3-7 7-7s7 3 7 7" /><path d="M17 4a4 4 0 0 1 0 8M22 21c0-3-2-5-4-6" /></Svg>

export const CheckIcon = (p: IconProps) => <Svg {...p}><path d="M20 6 9 17l-5-5" /></Svg>

export const BackIcon = (p: IconProps) => <Svg {...p}><path d="m15 18-6-6 6-6" /></Svg>

export const CloudOffIcon = (p: IconProps) => <Svg {...p}><path d="m2 2 20 20" /><path d="M5.8 8.2A6 6 0 0 0 7 20h10a4 4 0 0 0 1.6-.3M10 4.3A6 6 0 0 1 17.7 9 4 4 0 0 1 21 15" /></Svg>
