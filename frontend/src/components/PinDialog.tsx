import { Form, Input, Modal, message } from 'antd'
import { useEffect, useState } from 'react'
import { getApiMessage, usePins } from '../store/pins'

export function PinDialog() {
  const draft = usePins((s) => s.draft)
  const closeDraft = usePins((s) => s.closeDraft)
  const submitDraft = usePins((s) => s.submitDraft)
  const [alias, setAlias] = useState('')
  const [value, setValue] = useState('')
  const [comment, setComment] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!draft) {
      return
    }
    setAlias(draft.alias)
    setValue(draft.value)
    setComment(draft.comment)
  }, [draft])

  return (
    <Modal
      title={draft?.id ? 'Edit pin' : 'Pin resource'}
      open={Boolean(draft)}
      okText={draft?.id ? 'Save' : 'Pin'}
      confirmLoading={saving}
      onCancel={() => {
        if (!saving) {
          closeDraft()
        }
      }}
      onOk={async () => {
        if (!alias.trim() || !value.trim()) {
          message.error('Alias and value are required')
          return
        }
        usePins.setState((state) =>
          state.draft
            ? { draft: { ...state.draft, alias, value, comment } }
            : state,
        )
        setSaving(true)
        try {
          await submitDraft()
          message.success(draft?.id ? 'Pin updated' : 'Pinned')
        } catch (error) {
          message.error(getApiMessage(error, 'Could not save pin'))
        } finally {
          setSaving(false)
        }
      }}
      destroyOnClose
    >
      <Form layout="vertical" requiredMark={false} style={{ marginTop: 8 }}>
        <Form.Item label="As" extra="Name used to match path/query params">
          <Input
            value={alias}
            placeholder="workspaceIdGuid"
            maxLength={100}
            onChange={(event) => setAlias(event.target.value)}
            autoFocus
          />
        </Form.Item>
        <Form.Item label="Value">
          <Input.TextArea
            value={value}
            placeholder="GUID or id"
            maxLength={500}
            autoSize={{ minRows: 1, maxRows: 4 }}
            onChange={(event) => setValue(event.target.value)}
          />
        </Form.Item>
        <Form.Item label="Comment" extra="Optional note for yourself">
          <Input
            value={comment}
            placeholder="EU sandbox · Alex"
            maxLength={500}
            onChange={(event) => setComment(event.target.value)}
          />
        </Form.Item>
      </Form>
    </Modal>
  )
}
