import boatImg from '../../../assets/images/waterbus.jpg'

/** Ảnh tàu buýt đường sông thu nhỏ trên thẻ chuyến (chỉ để trang trí nên không có chữ thay thế). */
export default function BoatThumb() {
  return (
    <img
      src={boatImg}
      alt=""
      loading="lazy"
      className="h-14 w-[4.5rem] shrink-0 rounded-xl object-cover"
    />
  )
}
