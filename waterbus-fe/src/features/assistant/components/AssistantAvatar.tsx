import { Icon } from '../../../components/ui'

export default function AssistantAvatar() {
  return (
    <span
      aria-hidden="true"
      className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-deep-river text-sky-aqua"
    >
      <Icon name="bolt" className="text-[16px]" />
    </span>
  )
}
