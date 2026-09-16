import { Modal } from 'antd'
import { useState } from 'react'
import { IconHelp } from '../icons'

export function PersistenceHelp() {
  const [open, setOpen] = useState(false)

  return (
    <>
      <button
        type="button"
        className="btn btn-ghost btn-compact"
        title="What is saved when you close a tab"
        onClick={() => setOpen(true)}
      >
        <IconHelp size={16} />
        Help
      </button>
      <Modal
        title="What is saved"
        open={open}
        onCancel={() => setOpen(false)}
        footer={null}
        width={520}
        destroyOnClose
      >
        <div className="persistence-help">
          <section>
            <h3>Closing a workbench tab (×)</h3>
            <p>Lost for that tab: URL, headers draft, body, parameter values, and the last response.</p>
          </section>
          <section>
            <h3>Browser refresh (F5)</h3>
            <p>
              Kept in this browser session: open tabs (body, params, last response), environment, region, selected
              service/endpoint, and tokens by scope. Catalog search tabs also stay.
            </p>
          </section>
          <section>
            <h3>Longer-lived</h3>
            <ul>
              <li>Pins — server (shared across sessions)</li>
              <li>Favorites — this browser (localStorage)</li>
              <li>Request history — server</li>
              <li>Catalog / services.yaml — server</li>
            </ul>
          </section>
          <section>
            <h3>Not saved</h3>
            <p>UI-only state: Diff open, header expand, JSON fullscreen / tree mode, pending Replay until applied.</p>
          </section>
          <section>
            <h3>Logout / new browser session</h3>
            <p>Session workbench state is cleared. Pins, history, and favorites remain as above.</p>
          </section>
        </div>
      </Modal>
    </>
  )
}
