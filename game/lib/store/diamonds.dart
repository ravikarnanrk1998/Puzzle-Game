import 'package:flutter/material.dart';
import 'store_page.dart';
import 'store_service.dart';

const Color kDiamondColor = Colors.lightBlueAccent;

void openStore(BuildContext context) {
  Navigator.push(context, MaterialPageRoute(builder: (_) => const StorePage()));
}

/// A small diamond-icon-and-amount price tag.
class DiamondPrice extends StatelessWidget {
  final int amount;
  final double size;
  final Color color;
  const DiamondPrice(
    this.amount, {
    super.key,
    this.size = 14,
    this.color = kDiamondColor,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.diamond, color: color, size: size),
        const SizedBox(width: 3),
        Text(
          '$amount',
          style: TextStyle(
            color: color,
            fontSize: size,
            fontWeight: FontWeight.w900,
          ),
        ),
      ],
    );
  }
}

/// The player's diamond balance. Tapping it opens the store.
class DiamondBalance extends StatelessWidget {
  const DiamondBalance({super.key});

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: StoreService.instance,
      builder: (context, _) => GestureDetector(
        onTap: () => openStore(context),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
          decoration: BoxDecoration(
            color: Colors.black.withValues(alpha: 0.35),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: kDiamondColor.withValues(alpha: 0.6)),
          ),
          child: DiamondPrice(StoreService.instance.diamonds, size: 15),
        ),
      ),
    );
  }
}

/// [text] as is when [lockId] is unlocked, otherwise with a lock and the
/// unlock price. For buttons that start a level.
class LockedLabel extends StatelessWidget {
  final String text;
  final String lockId;
  const LockedLabel(this.text, {super.key, required this.lockId});

  @override
  Widget build(BuildContext context) {
    if (StoreService.instance.isUnlocked(lockId)) return Text(text);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.lock, size: 16),
        const SizedBox(width: 6),
        Text(text),
        const SizedBox(width: 10),
        const DiamondPrice(StoreService.unlockCost),
      ],
    );
  }
}

/// Returns true right away if [lockId] is unlocked. Otherwise asks the
/// player to spend diamonds on it, and returns whether they did.
Future<bool> confirmDiamondUnlock(
  BuildContext context, {
  required String lockId,
  required String name,
}) async {
  if (StoreService.instance.isUnlocked(lockId)) return true;
  final unlocked = await showDialog<bool>(
    context: context,
    builder: (_) => _UnlockDialog(lockId: lockId, name: name),
  );
  return unlocked ?? false;
}

class _UnlockDialog extends StatelessWidget {
  final String lockId;
  final String name;
  const _UnlockDialog({required this.lockId, required this.name});

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: Colors.transparent,
      child: ListenableBuilder(
        listenable: StoreService.instance,
        builder: (context, _) {
          final store = StoreService.instance;
          final missing = StoreService.unlockCost - store.diamonds;
          return Container(
            padding: const EdgeInsets.fromLTRB(26, 30, 26, 18),
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: [
                  Color.alphaBlend(
                    kDiamondColor.withValues(alpha: 0.25),
                    const Color(0xFF14161F),
                  ),
                  const Color(0xFF0A0C12),
                ],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
              borderRadius: BorderRadius.circular(28),
              border: Border.all(
                color: kDiamondColor.withValues(alpha: 0.5),
                width: 1.4,
              ),
              boxShadow: [
                BoxShadow(
                  color: kDiamondColor.withValues(alpha: 0.3),
                  blurRadius: 36,
                  spreadRadius: 3,
                ),
              ],
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.lock_open, color: kDiamondColor, size: 44),
                const SizedBox(height: 12),
                Text(
                  'Unlock $name',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 22,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                const SizedBox(height: 10),
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      'You have ',
                      style: TextStyle(
                        color: Colors.white.withValues(alpha: 0.7),
                      ),
                    ),
                    DiamondPrice(store.diamonds),
                  ],
                ),
                if (missing > 0) ...[
                  const SizedBox(height: 4),
                  Text(
                    'You need $missing more to unlock this.',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: Colors.white.withValues(alpha: 0.7),
                    ),
                  ),
                ],
                const SizedBox(height: 22),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton(
                    onPressed: () {
                      if (missing > 0) {
                        openStore(context);
                      } else if (store.unlock(lockId)) {
                        Navigator.of(context).pop(true);
                      }
                    },
                    style: ElevatedButton.styleFrom(
                      backgroundColor: kDiamondColor,
                      foregroundColor: Colors.black,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(30),
                      ),
                    ),
                    child: missing > 0
                        ? const Text(
                            'GET DIAMONDS',
                            style: TextStyle(fontWeight: FontWeight.w900),
                          )
                        : const Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                'UNLOCK FOR ',
                                style: TextStyle(fontWeight: FontWeight.w900),
                              ),
                              DiamondPrice(
                                StoreService.unlockCost,
                                size: 15,
                                color: Colors.black,
                              ),
                            ],
                          ),
                  ),
                ),
                const SizedBox(height: 6),
                TextButton(
                  onPressed: () => Navigator.of(context).pop(false),
                  child: const Text(
                    'Not now',
                    style: TextStyle(color: Colors.white60),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
