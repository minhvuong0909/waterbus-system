import { Icon } from '../../../components/ui'
import { useOnboardPlayer } from '../hooks/useOnboardPlayer'

/**
 * Loa của tàu (BR-EXP-04) không có giao diện riêng: phát thông báo bến và thuyết minh địa danh do backend kích hoạt theo vị trí tàu,
 * tiếng Việt trước rồi tiếng Anh. Chỉ hiện một dải nhỏ ở góc dưới khi đang phát để thuyền trưởng biết loa đang nói gì.
 */
export default function BoatSpeaker() {
  const { nowPlaying } = useOnboardPlayer(true)
  if (!nowPlaying) return null
  return (
    <div className="cap-speaker" role="status">
      <Icon name="volume_up" className="text-xl" />
      <div>
        <strong>
          {nowPlaying.subjectName} ({nowPlaying.language})
        </strong>
        <span>{nowPlaying.text}</span>
      </div>
    </div>
  )
}
