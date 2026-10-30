import { PageHeader } from '../../features/admin/components/AdminUi'
import { useDocumentTitle } from '../../hooks'

// Backend chưa có API quản lý bảng giá (chỉ `GET /fares` công khai, đọc), nên màn này không sửa được giá.
export default function FaresPage() {
  useDocumentTitle('Fares')
  return (
    <div className="p-6">
      <PageHeader
        title="Fares"
        description="Backend chưa có API quản lý bảng giá. Giá vé thật được lấy từ API sơ đồ ghế theo đoạn chuyến."
      />
    </div>
  )
}
