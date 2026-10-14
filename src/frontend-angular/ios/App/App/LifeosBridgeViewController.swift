import Capacitor

final class LifeosBridgeViewController: CAPBridgeViewController {
    override func capacitorDidLoad() {
        bridge?.registerPluginInstance(LifeosNativeBridgePlugin())
    }
}
