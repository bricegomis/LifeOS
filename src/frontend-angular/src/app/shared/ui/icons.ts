import { inject, provideAppInitializer } from '@angular/core'
import { DomSanitizer } from '@angular/platform-browser'
import { MatIconRegistry } from '@angular/material/icon'
import MenuIcon from '@material-symbols/svg-400/rounded/menu.svg'
import CloseIcon from '@material-symbols/svg-400/rounded/close.svg'
import ArrowBackIcon from '@material-symbols/svg-400/rounded/arrow_back.svg'
import ArrowForwardIcon from '@material-symbols/svg-400/rounded/arrow_forward.svg'
import ChevronRightIcon from '@material-symbols/svg-400/rounded/chevron_right.svg'
import KeyboardArrowDownIcon from '@material-symbols/svg-400/rounded/keyboard_arrow_down.svg'
import SettingsIcon from '@material-symbols/svg-400/rounded/settings.svg'
import LogoutIcon from '@material-symbols/svg-400/rounded/logout.svg'
import RestaurantIcon from '@material-symbols/svg-400/rounded/restaurant.svg'
import FitnessCenterIcon from '@material-symbols/svg-400/rounded/fitness_center.svg'
import AddIcon from '@material-symbols/svg-400/rounded/add.svg'
import EditIcon from '@material-symbols/svg-400/rounded/edit.svg'
import DeleteIcon from '@material-symbols/svg-400/rounded/delete.svg'
import ArchiveIcon from '@material-symbols/svg-400/rounded/archive.svg'
import SearchIcon from '@material-symbols/svg-400/rounded/search.svg'
import CheckIcon from '@material-symbols/svg-400/rounded/check.svg'
import BiotechIcon from '@material-symbols/svg-400/rounded/biotech.svg'
import DescriptionIcon from '@material-symbols/svg-400/rounded/description.svg'
import PictureAsPdfIcon from '@material-symbols/svg-400/rounded/picture_as_pdf.svg'
import EventIcon from '@material-symbols/svg-400/rounded/event.svg'
import AttachFileIcon from '@material-symbols/svg-400/rounded/attach_file.svg'
import UploadFileIcon from '@material-symbols/svg-400/rounded/upload_file.svg'
import ImageIcon from '@material-symbols/svg-400/rounded/image.svg'
import UndoIcon from '@material-symbols/svg-400/rounded/undo.svg'
import PlayArrowIcon from '@material-symbols/svg-400/rounded/play_arrow.svg'
import ScheduleIcon from '@material-symbols/svg-400/rounded/schedule.svg'
import RateReviewIcon from '@material-symbols/svg-400/rounded/rate_review.svg'
import ArrowUpwardIcon from '@material-symbols/svg-400/rounded/arrow_upward.svg'
import ArrowDownwardIcon from '@material-symbols/svg-400/rounded/arrow_downward.svg'
import RemoveIcon from '@material-symbols/svg-400/rounded/remove.svg'
import EventRepeatIcon from '@material-symbols/svg-400/rounded/event_repeat.svg'
import LocalFireDepartmentIcon from '@material-symbols/svg-400/rounded/local_fire_department.svg'
import FlagIcon from '@material-symbols/svg-400/rounded/flag.svg'
import DirectionsRunIcon from '@material-symbols/svg-400/rounded/directions_run.svg'
import TuneIcon from '@material-symbols/svg-400/rounded/tune.svg'
import PhotoCameraIcon from '@material-symbols/svg-400/rounded/photo_camera.svg'
import HistoryIcon from '@material-symbols/svg-400/rounded/history.svg'
import ShowChartIcon from '@material-symbols/svg-400/rounded/show_chart.svg'
import TodayIcon from '@material-symbols/svg-400/rounded/today.svg'
import ErrorIcon from '@material-symbols/svg-400/rounded/error.svg'
import TrophyIcon from '@material-symbols/svg-400/rounded/trophy.svg'
import CalendarMonthIcon from '@material-symbols/svg-400/rounded/calendar_month.svg'
import ReceiptLongIcon from '@material-symbols/svg-400/rounded/receipt_long.svg'
import StorefrontIcon from '@material-symbols/svg-400/rounded/storefront.svg'
import SellIcon from '@material-symbols/svg-400/rounded/sell.svg'
import NutritionIcon from '@material-symbols/svg-400/rounded/nutrition.svg'
import MenuBookIcon from '@material-symbols/svg-400/rounded/menu_book.svg'
import CheckCircleIcon from '@material-symbols/svg-400/rounded/check_circle.svg'
import PhotoLibraryIcon from '@material-symbols/svg-400/rounded/photo_library.svg'
import RotateRightIcon from '@material-symbols/svg-400/rounded/rotate_right.svg'
import OpenInNewIcon from '@material-symbols/svg-400/rounded/open_in_new.svg'
import ContentCopyIcon from '@material-symbols/svg-400/rounded/content_copy.svg'
import AddAPhotoIcon from '@material-symbols/svg-400/rounded/add_a_photo.svg'
import PersonIcon from '@material-symbols/svg-400/rounded/person.svg'
import LinkOffIcon from '@material-symbols/svg-400/rounded/link_off.svg'
import SyncIcon from '@material-symbols/svg-400/rounded/sync.svg'
import TaskAltIcon from '@material-symbols/svg-400/rounded/task_alt.svg'
import RadioButtonUncheckedIcon from '@material-symbols/svg-400/rounded/radio_button_unchecked.svg'
import TimerIcon from '@material-symbols/svg-400/rounded/timer.svg'
import NotesIcon from '@material-symbols/svg-400/rounded/notes.svg'
import BlockIcon from '@material-symbols/svg-400/rounded/block.svg'
import RedoIcon from '@material-symbols/svg-400/rounded/redo.svg'
import PauseIcon from '@material-symbols/svg-400/rounded/pause.svg'
import FolderIcon from '@material-symbols/svg-400/rounded/folder.svg'
import FavoriteIcon from '@material-symbols/svg-400/rounded/favorite.svg'
import MedicationIcon from '@material-symbols/svg-400/rounded/medication.svg'
import ShoppingCartIcon from '@material-symbols/svg-400/rounded/shopping_cart.svg'
import ChecklistIcon from '@material-symbols/svg-400/rounded/checklist.svg'
import SentimentSatisfiedIcon from '@material-symbols/svg-400/rounded/sentiment_satisfied.svg'
import HomeIcon from '@material-symbols/svg-400/rounded/home.svg'
import BedtimeIcon from '@material-symbols/svg-400/rounded/bedtime.svg'
import MonitorHeartIcon from '@material-symbols/svg-400/rounded/monitor_heart.svg'
import BatteryChargingFullIcon from '@material-symbols/svg-400/rounded/battery_charging_full.svg'
import DirectionsBikeIcon from '@material-symbols/svg-400/rounded/directions_bike.svg'
import DirectionsWalkIcon from '@material-symbols/svg-400/rounded/directions_walk.svg'
import PoolIcon from '@material-symbols/svg-400/rounded/pool.svg'
import HikingIcon from '@material-symbols/svg-400/rounded/hiking.svg'
import SportsGymnasticsIcon from '@material-symbols/svg-400/rounded/sports_gymnastics.svg'
import WatchIcon from '@material-symbols/svg-400/rounded/watch.svg'
import LandscapeIcon from '@material-symbols/svg-400/rounded/landscape.svg'
import SpeedIcon from '@material-symbols/svg-400/rounded/speed.svg'
import StraightenIcon from '@material-symbols/svg-400/rounded/straighten.svg'
import TrendingUpIcon from '@material-symbols/svg-400/rounded/trending_up.svg'
import AccountBalanceWalletIcon from '@material-symbols/svg-400/rounded/account_balance_wallet.svg'
import SavingsIcon from '@material-symbols/svg-400/rounded/savings.svg'
import BoltIcon from '@material-symbols/svg-400/rounded/bolt.svg'
import SwapHorizIcon from '@material-symbols/svg-400/rounded/swap_horiz.svg'
import PaymentsIcon from '@material-symbols/svg-400/rounded/payments.svg'
import TrendingDownIcon from '@material-symbols/svg-400/rounded/trending_down.svg'
import TrendingFlatIcon from '@material-symbols/svg-400/rounded/trending_flat.svg'
import RuleIcon from '@material-symbols/svg-400/rounded/rule.svg'
import ChevronLeftIcon from '@material-symbols/svg-400/rounded/chevron_left.svg'
import HelpIcon from '@material-symbols/svg-400/rounded/help.svg'
import TouchAppIcon from '@material-symbols/svg-400/rounded/touch_app.svg'
import SmartToyIcon from '@material-symbols/svg-400/rounded/smart_toy.svg'
import KeyIcon from '@material-symbols/svg-400/rounded/key.svg'
import RestartAltIcon from '@material-symbols/svg-400/rounded/restart_alt.svg'
import MarkEmailUnreadIcon from '@material-symbols/svg-400/rounded/mark_email_unread.svg'
import SyncProblemIcon from '@material-symbols/svg-400/rounded/sync_problem.svg'
import TimerOffIcon from '@material-symbols/svg-400/rounded/timer_off.svg'
import KeyOffIcon from '@material-symbols/svg-400/rounded/key_off.svg'
import AirIcon from '@material-symbols/svg-400/rounded/air.svg'
import PsychologyIcon from '@material-symbols/svg-400/rounded/psychology.svg'
import SportsIcon from '@material-symbols/svg-400/rounded/sports.svg'
import SelfImprovementIcon from '@material-symbols/svg-400/rounded/self_improvement.svg'

/**
 * Material Symbols used by the app (ADR 0016), bundled as SVG so only these icons ship and nothing is
 * fetched from Google. Use them with `<mat-icon svgIcon="name" />`; add an icon here before using it.
 */
export const ICONS: Record<string, string> = {
  menu: MenuIcon,
  close: CloseIcon,
  arrow_back: ArrowBackIcon,
  arrow_forward: ArrowForwardIcon,
  chevron_right: ChevronRightIcon,
  keyboard_arrow_down: KeyboardArrowDownIcon,
  settings: SettingsIcon,
  logout: LogoutIcon,
  restaurant: RestaurantIcon,
  fitness_center: FitnessCenterIcon,
  add: AddIcon,
  edit: EditIcon,
  delete: DeleteIcon,
  archive: ArchiveIcon,
  search: SearchIcon,
  check: CheckIcon,
  biotech: BiotechIcon,
  description: DescriptionIcon,
  picture_as_pdf: PictureAsPdfIcon,
  event: EventIcon,
  upload_file: UploadFileIcon,
  attach_file: AttachFileIcon,
  image: ImageIcon,
  undo: UndoIcon,
  play_arrow: PlayArrowIcon,
  schedule: ScheduleIcon,
  rate_review: RateReviewIcon,
  arrow_upward: ArrowUpwardIcon,
  arrow_downward: ArrowDownwardIcon,
  remove: RemoveIcon,
  event_repeat: EventRepeatIcon,
  local_fire_department: LocalFireDepartmentIcon,
  flag: FlagIcon,
  directions_run: DirectionsRunIcon,
  tune: TuneIcon,
  photo_camera: PhotoCameraIcon,
  history: HistoryIcon,
  show_chart: ShowChartIcon,
  today: TodayIcon,
  error: ErrorIcon,
  trophy: TrophyIcon,
  calendar_month: CalendarMonthIcon,
  receipt_long: ReceiptLongIcon,
  storefront: StorefrontIcon,
  sell: SellIcon,
  nutrition: NutritionIcon,
  menu_book: MenuBookIcon,
  check_circle: CheckCircleIcon,
  photo_library: PhotoLibraryIcon,
  rotate_right: RotateRightIcon,
  open_in_new: OpenInNewIcon,
  content_copy: ContentCopyIcon,
  add_a_photo: AddAPhotoIcon,
  person: PersonIcon,
  link_off: LinkOffIcon,
  sync: SyncIcon,
  task_alt: TaskAltIcon,
  radio_button_unchecked: RadioButtonUncheckedIcon,
  timer: TimerIcon,
  notes: NotesIcon,
  block: BlockIcon,
  redo: RedoIcon,
  pause: PauseIcon,
  folder: FolderIcon,
  favorite: FavoriteIcon,
  medication: MedicationIcon,
  shopping_cart: ShoppingCartIcon,
  checklist: ChecklistIcon,
  sentiment_satisfied: SentimentSatisfiedIcon,
  home: HomeIcon,
  bedtime: BedtimeIcon,
  monitor_heart: MonitorHeartIcon,
  battery_charging_full: BatteryChargingFullIcon,
  directions_bike: DirectionsBikeIcon,
  directions_walk: DirectionsWalkIcon,
  pool: PoolIcon,
  hiking: HikingIcon,
  sports_gymnastics: SportsGymnasticsIcon,
  watch: WatchIcon,
  landscape: LandscapeIcon,
  speed: SpeedIcon,
  straighten: StraightenIcon,
  trending_up: TrendingUpIcon,
  key: KeyIcon,
  restart_alt: RestartAltIcon,
  mark_email_unread: MarkEmailUnreadIcon,
  sync_problem: SyncProblemIcon,
  timer_off: TimerOffIcon,
  key_off: KeyOffIcon,
  air: AirIcon,
  psychology: PsychologyIcon,
  sports: SportsIcon,
  self_improvement: SelfImprovementIcon,
  account_balance_wallet: AccountBalanceWalletIcon,
  savings: SavingsIcon,
  bolt: BoltIcon,
  swap_horiz: SwapHorizIcon,
  payments: PaymentsIcon,
  trending_down: TrendingDownIcon,
  trending_flat: TrendingFlatIcon,
  rule: RuleIcon,
  chevron_left: ChevronLeftIcon,
  help: HelpIcon,
  touch_app: TouchAppIcon,
  smart_toy: SmartToyIcon,
}

export function provideLifeosIcons() {
  return provideAppInitializer(() => {
    const registry = inject(MatIconRegistry)
    const sanitizer = inject(DomSanitizer)
    for (const [name, svg] of Object.entries(ICONS)) registry.addSvgIconLiteral(name, sanitizer.bypassSecurityTrustHtml(svg))
  })
}
