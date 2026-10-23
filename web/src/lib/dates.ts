/** API'den gelen "2005-06-01" tarihini "01.06.2005" olarak gösterir (saat dilimine göre kaymadan). */
export function formatDate(isoDate: string) {
  const [year, month, day] = isoDate.split("-");
  return year && month && day ? `${day}.${month}.${year}` : isoDate;
}
