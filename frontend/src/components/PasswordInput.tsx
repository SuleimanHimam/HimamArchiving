import { useState, forwardRef } from 'react'
import { Eye, EyeOff } from 'lucide-react'
import './passwordinput.css'

type Props = React.InputHTMLAttributes<HTMLInputElement>

const PasswordInput = forwardRef<HTMLInputElement, Props>(function PasswordInput(
  { className, ...rest },
  ref,
) {
  const [show, setShow] = useState(false)
  return (
    <div className={`pwfield ${className ?? ''}`}>
      <input {...rest} ref={ref} type={show ? 'text' : 'password'} />
      <button
        type="button"
        className="pwfield__toggle"
        onClick={() => setShow((s) => !s)}
        tabIndex={-1}
        aria-label={show ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'}
        title={show ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'}
      >
        {show ? <EyeOff size={16} /> : <Eye size={16} />}
      </button>
    </div>
  )
})

export default PasswordInput
