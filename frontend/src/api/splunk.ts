export function headerObservabilityUrl(
  template: string | null | undefined,
  headerName: string,
  headerValue: string,
): string | null {
  const pattern = template?.trim()
  const value = headerValue.trim()
  if (!pattern || !value || !headerName.trim()) {
    return null
  }

  const token = `#${headerName}#`
  if (!pattern.toLowerCase().includes(token.toLowerCase())) {
    return null
  }

  const escaped = headerName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  return pattern.replace(new RegExp(`#${escaped}#`, 'gi'), encodeURIComponent(value))
}
