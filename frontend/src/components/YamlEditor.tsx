import Editor from '@monaco-editor/react'

type Props = {
  value: string
  onChange: (value: string) => void
  readOnly?: boolean
  height?: string | number
}

export function YamlEditor({ value, onChange, readOnly, height = 'min(62vh, 720px)' }: Props) {
  return (
    <div className="yaml-monaco">
      <Editor
        height={height}
        defaultLanguage="yaml"
        theme="vs-dark"
        value={value}
        onChange={(next: string | undefined) => onChange(next ?? '')}
        options={{
          readOnly: Boolean(readOnly),
          fontSize: 13,
          fontFamily: 'JetBrains Mono, ui-monospace, monospace',
          lineNumbers: 'on',
          minimap: { enabled: false },
          scrollBeyondLastLine: false,
          wordWrap: 'on',
          tabSize: 2,
          automaticLayout: true,
          renderLineHighlight: 'line',
          padding: { top: 12, bottom: 12 },
        }}
      />
    </div>
  )
}
