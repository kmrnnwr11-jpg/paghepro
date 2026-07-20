import Foundation
import Security
import Capacitor

@objc(SecureSessionPlugin)
public final class SecureSessionPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "SecureSessionPlugin"
    public let jsName = "SecureSession"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "set", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "get", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "clear", returnType: CAPPluginReturnPromise)
    ]

    private let servizio = "it.paghepro.session.refresh.v1"
    private let account = "refresh"
    private let marcatore = "PagheProSecureSession.InstallazioneV1"

    public override func load() {
        super.load()
        // Il Keychain può sopravvivere alla disinstallazione. UserDefaults no: al primo avvio
        // della nuova installazione eliminiamo quindi qualunque sessione rimasta sul dispositivo.
        if !UserDefaults.standard.bool(forKey: marcatore) {
            elimina()
            UserDefaults.standard.set(true, forKey: marcatore)
        }
    }

    @objc func set(_ call: CAPPluginCall) {
        guard let valore = call.getString("value"), !valore.isEmpty,
              let dati = valore.data(using: .utf8) else {
            call.reject("Valore sessione mancante.")
            return
        }
        elimina()
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: servizio,
            kSecAttrAccount as String: account,
            kSecAttrAccessible as String: kSecAttrAccessibleWhenUnlockedThisDeviceOnly,
            kSecValueData as String: dati
        ]
        let stato = SecItemAdd(query as CFDictionary, nil)
        guard stato == errSecSuccess else {
            call.reject("Impossibile proteggere la sessione.")
            return
        }
        call.resolve()
    }

    @objc func get(_ call: CAPPluginCall) {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: servizio,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]
        var risultato: CFTypeRef?
        let stato = SecItemCopyMatching(query as CFDictionary, &risultato)
        if stato == errSecItemNotFound {
            call.resolve()
            return
        }
        guard stato == errSecSuccess,
              let dati = risultato as? Data,
              let valore = String(data: dati, encoding: .utf8) else {
            elimina()
            call.reject("Sessione locale non leggibile.")
            return
        }
        call.resolve(["value": valore])
    }

    @objc func clear(_ call: CAPPluginCall) {
        elimina()
        call.resolve()
    }

    private func elimina() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: servizio,
            kSecAttrAccount as String: account
        ]
        SecItemDelete(query as CFDictionary)
    }
}
