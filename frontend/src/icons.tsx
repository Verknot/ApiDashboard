import type { SVGProps } from 'react'

type IconProps = Omit<SVGProps<SVGSVGElement>, 'stroke'> & {
  size?: number
  stroke?: number
  filled?: boolean
}

function TablerIcon({
  paths,
  size = 20,
  stroke = 1.5,
  filled = false,
  ...rest
}: IconProps & { paths: string[]; filled?: boolean }) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={filled ? 'currentColor' : 'none'}
      stroke={filled ? 'none' : 'currentColor'}
      strokeWidth={stroke}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      {...rest}
    >
      {paths.map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  )
}

/** Tabler Icons v3.35.0, MIT. https://github.com/tabler/tabler-icons */
export const IconApi = (p: IconProps) => (
  <TablerIcon {...p} paths={['M4 13h5', 'M12 16v-8h3a2 2 0 0 1 2 2v1a2 2 0 0 1 -2 2h-3', 'M20 8v8', 'M9 16v-5.5a2.5 2.5 0 0 0 -5 0v5.5']} />
)
export const IconHistory = (p: IconProps) => (
  <TablerIcon {...p} paths={['M12 8l0 4l2 2', 'M3.05 11a9 9 0 1 1 .5 4m-.5 5v-5h5']} />
)
export const IconSettings = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={[
      'M10.325 4.317c.426 -1.756 2.924 -1.756 3.35 0a1.724 1.724 0 0 0 2.573 1.066c1.543 -.94 3.31 .826 2.37 2.37a1.724 1.724 0 0 0 1.065 2.572c1.756 .426 1.756 2.924 0 3.35a1.724 1.724 0 0 0 -1.066 2.573c.94 1.543 -.826 3.31 -2.37 2.37a1.724 1.724 0 0 0 -2.572 1.065c-.426 1.756 -2.924 1.756 -3.35 0a1.724 1.724 0 0 0 -2.573 -1.066c-1.543 .94 -3.31 -.826 -2.37 -2.37a1.724 1.724 0 0 0 -1.065 -2.572c-1.756 -.426 -1.756 -2.924 0 -3.35a1.724 1.724 0 0 0 1.066 -2.573c-.94 -1.543 .826 -3.31 2.37 -2.37c1 .608 2.296 .07 2.572 -1.065z',
      'M9 12a3 3 0 1 0 6 0a3 3 0 0 0 -6 0',
    ]}
  />
)
export const IconLogout = (p: IconProps) => (
  <TablerIcon {...p} paths={['M14 8v-2a2 2 0 0 0 -2 -2h-7a2 2 0 0 0 -2 2v12a2 2 0 0 0 2 2h7a2 2 0 0 0 2 -2v-2', 'M9 12h12l-3 -3', 'M18 15l3 -3']} />
)
export const IconUser = (p: IconProps) => (
  <TablerIcon {...p} paths={['M8 7a4 4 0 1 0 8 0a4 4 0 0 0 -8 0', 'M6 21v-2a4 4 0 0 1 4 -4h4a4 4 0 0 1 4 4v2']} />
)
export const IconSearch = (p: IconProps) => (
  <TablerIcon {...p} paths={['M10 10m-7 0a7 7 0 1 0 14 0a7 7 0 1 0 -14 0', 'M21 21l-6 -6']} />
)
export const IconMapPin = (p: IconProps) => (
  <TablerIcon {...p} paths={['M9 11a3 3 0 1 0 6 0a3 3 0 0 0 -6 0', 'M17.657 16.657l-4.243 4.243a2 2 0 0 1 -2.827 0l-4.244 -4.243a8 8 0 1 1 11.314 0z']} />
)
export const IconDeviceFloppy = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={['M6 4h10l4 4v10a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-12a2 2 0 0 1 2 -2', 'M12 14m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0', 'M14 4l0 4l-6 0l0 -4']}
  />
)
export const IconRefresh = (p: IconProps) => (
  <TablerIcon {...p} paths={['M20 11a8.1 8.1 0 0 0 -15.5 -2m-.5 -4v4h4', 'M4 13a8.1 8.1 0 0 0 15.5 2m.5 4v-4h-4']} />
)
export const IconChevronRight = (p: IconProps) => (
  <TablerIcon {...p} paths={['M9 6l6 6l-6 6']} />
)
export const IconSend = (p: IconProps) => (
  <TablerIcon {...p} paths={['M10 14l11 -11', 'M21 3l-6.5 18a.55 .55 0 0 1 -1 0l-3.5 -7l-7 -3.5a.55 .55 0 0 1 0 -1l18 -6.5']} />
)
export const IconTag = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={[
      'M7.5 7.5m-1 0a1 1 0 1 0 2 0a1 1 0 1 0 -2 0',
      'M3 6v5.172a2 2 0 0 0 .586 1.414l7.71 7.71a2.41 2.41 0 0 0 3.408 0l5.592 -5.592a2.41 2.41 0 0 0 0 -3.408l-7.71 -7.71a2 2 0 0 0 -1.414 -.586h-5.172a3 3 0 0 0 -3 3z',
    ]}
  />
)
export const IconStar = (p: IconProps) => (
  <TablerIcon
    {...p}
    filled={p.filled}
    paths={['M12 17.75l-6.172 3.245l1.179 -6.873l-5 -4.867l6.9 -1l3.086 -6.253l3.086 6.253l6.9 1l-5 4.867l1.179 6.873z']}
  />
)
export const IconPlus = (p: IconProps) => <TablerIcon {...p} paths={['M12 5l0 14', 'M5 12l14 0']} />
export const IconX = (p: IconProps) => <TablerIcon {...p} paths={['M18 6l-12 12', 'M6 6l12 12']} />
export const IconTrash = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={[
      'M4 7l16 0',
      'M10 11l0 6',
      'M14 11l0 6',
      'M5 7l1 12a2 2 0 0 0 2 2h8a2 2 0 0 0 2 -2l1 -12',
      'M9 7v-3a1 1 0 0 1 1 -1h4a1 1 0 0 1 1 1v3',
    ]}
  />
)
export const IconPencil = (p: IconProps) => (
  <TablerIcon {...p} paths={['M4 20h4l10.5 -10.5a2.828 2.828 0 1 0 -4 -4l-10.5 10.5v4', 'M13.5 6.5l4 4']} />
)
export const IconAlertTriangle = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={['M12 9v4', 'M10.363 3.591l-8.106 13.534a1.914 1.914 0 0 0 1.636 2.871h16.214a1.914 1.914 0 0 0 1.636 -2.87l-8.106 -13.536a1.914 1.914 0 0 0 -3.274 0z', 'M12 16h.01']}
  />
)
export const IconLock = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={['M5 13a2 2 0 0 1 2 -2h10a2 2 0 0 1 2 2v6a2 2 0 0 1 -2 2h-10a2 2 0 0 1 -2 -2v-6z', 'M11 16a1 1 0 1 0 2 0a1 1 0 0 0 -2 0', 'M8 11v-4a4 4 0 1 1 8 0v4']}
  />
)
export const IconChevronLeft = (p: IconProps) => <TablerIcon {...p} paths={['M15 6l-6 6l6 6']} />
export const IconFileCode = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={['M14 3v4a1 1 0 0 0 1 1h4', 'M17 21h-10a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2h7l5 5v11a2 2 0 0 1 -2 2z', 'M10 13l-1 2l1 2', 'M14 13l1 2l-1 2']}
  />
)
export const IconCloudOff = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={[
      'M9.58 5.548c.24 -.11 .492 -.207 .752 -.286c1.88 -.572 3.956 -.193 5.444 1c1.488 1.19 2.162 3.007 1.77 4.769h.99c1.913 0 3.464 1.56 3.464 3.486c0 .957 -.383 1.824 -1.003 2.454m-2.997 1.033h-11.343c-2.572 -.004 -4.657 -2.011 -4.657 -4.487c0 -2.475 2.085 -4.482 4.657 -4.482c.13 -.582 .37 -1.128 .7 -1.62',
      'M3 3l18 18',
    ]}
  />
)
export const IconKey = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={[
      'M16.555 3.843l3.602 3.602a2.877 2.877 0 0 1 0 4.069l-2.643 2.643a2.877 2.877 0 0 1 -4.069 0l-.301 -.301l-6.558 6.558a2 2 0 0 1 -1.239 .578l-.175 .008h-1.172a1 1 0 0 1 -.993 -.883l-.007 -.117v-1.172a2 2 0 0 1 .467 -1.284l.119 -.13l.414 -.414h2v-2h2v-2l2.144 -2.144l-.301 -.301a2.877 2.877 0 0 1 0 -4.069l2.643 -2.643a2.877 2.877 0 0 1 4.069 0z',
      'M15 9h.01',
    ]}
  />
)
export const IconTerminal = (p: IconProps) => (
  <TablerIcon {...p} paths={['M8 9l3 3l-3 3', 'M13 15l3 0', 'M3 4m0 2a2 2 0 0 1 2 -2h14a2 2 0 0 1 2 2v12a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2z']} />
)
export const IconWorld = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={['M3 12a9 9 0 1 0 18 0a9 9 0 0 0 -18 0', 'M3.6 9h16.8', 'M3.6 15h16.8', 'M11.5 3a17 17 0 0 0 0 18', 'M12.5 3a17 17 0 0 1 0 18']}
  />
)
export const IconInbox = (p: IconProps) => (
  <TablerIcon {...p} paths={['M4 4m0 2a2 2 0 0 1 2 -2h12a2 2 0 0 1 2 2v12a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2z', 'M4 13h3l3 3h4l3 -3h3']} />
)
export const IconRoute = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={['M3 19a2 2 0 1 0 4 0a2 2 0 0 0 -4 0', 'M19 7a2 2 0 1 0 0 -4a2 2 0 0 0 0 4z', 'M11 19h5.5a3.5 3.5 0 0 0 0 -7h-8a3.5 3.5 0 0 1 0 -7h4.5']}
  />
)
export const IconExternalLink = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={['M12 6h-6a2 2 0 0 0 -2 2v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2 -2v-6', 'M11 13l9 -9', 'M15 4h5v5']}
  />
)
export const IconCopy = (p: IconProps) => (
  <TablerIcon {...p} paths={['M7 7m0 2.667a2.667 2.667 0 0 1 2.667 -2.667h8.666a2.667 2.667 0 0 1 2.667 2.667v8.666a2.667 2.667 0 0 1 -2.667 2.667h-8.666a2.667 2.667 0 0 1 -2.667 -2.667z', 'M4.012 16.737a2.005 2.005 0 0 1 -1.012 -1.737v-10c0 -1.1 .9 -2 2 -2h10c.75 0 1.158 .385 1.5 1']} />
)
export const IconEye = (p: IconProps) => (
  <TablerIcon {...p} paths={['M10 12a2 2 0 1 0 4 0a2 2 0 0 0 -4 0', 'M21 12c-2.4 4 -5.4 6 -9 6c-3.6 0 -6.6 -2 -9 -6c2.4 -4 5.4 -6 9 -6c3.6 0 6.6 2 9 6']} />
)
export const IconEyeOff = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={[
      'M10.585 10.587a2 2 0 0 0 2.829 2.828',
      'M16.681 16.673a8.717 8.717 0 0 1 -4.681 1.327c-3.6 0 -6.6 -2 -9 -6c1.272 -2.12 2.712 -3.678 4.32 -4.674m2.86 -1.146a9.055 9.055 0 0 1 1.82 -.18c3.6 0 6.6 2 9 6c-.666 1.11 -1.417 2.067 -2.248 2.87',
      'M3 3l18 18',
    ]}
  />
)
export const IconShieldLock = (p: IconProps) => (
  <TablerIcon
    {...p}
    paths={[
      'M12 3a12 12 0 0 0 8.5 3a12 12 0 0 1 -8.5 15a12 12 0 0 1 -8.5 -15a12 12 0 0 0 8.5 -3',
      'M12 11m-1 0a1 1 0 1 0 2 0a1 1 0 1 0 -2 0',
      'M12 12l0 2.5',
    ]}
  />
)
