const CONVERSATION_ID_TOKEN = '#ConversationId#'

/** Headers that carry the same correlation id as #ConversationId# in splunk_url. */
function isConversationIdHeader(headerName: string): boolean {
  const n = headerName.trim().toLowerCase().replaceAll('_', '-')
  return (
    n === 'conversationid' ||
    n === 'conversation-id' ||
    n.endsWith('-conversation-id') ||
    n.includes('conversation-id')
  )
}

export function headerObservabilityUrl(
  template: string | null | undefined,
  headerName: string,
  headerValue: string,
): string | null {
  const pattern = template?.trim()
  const value = headerValue.trim()
  const name = headerName.trim()
  if (!pattern || !value || !name) {
    return null
  }

  const exactToken = `#${name}#`
  if (pattern.toLowerCase().includes(exactToken.toLowerCase())) {
    const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    return pattern.replace(new RegExp(`#${escaped}#`, 'gi'), encodeURIComponent(value))
  }

  // YAML usually uses #ConversationId#, APIs return x-conversation-id / x-b2b-jaeger-conversation-id
  if (
    isConversationIdHeader(name) &&
    pattern.toLowerCase().includes(CONVERSATION_ID_TOKEN.toLowerCase())
  ) {
    return pattern.replace(new RegExp(CONVERSATION_ID_TOKEN, 'gi'), encodeURIComponent(value))
  }

  return null
}
