\#serving: 

\#shir cohen 207365024 , yana zlatin 314833328

class Substitutor:

&nbsp;   """Base class for all Enigma components that substitute letters."""



&nbsp;   def letter\_to\_index(self, letter: str) -> int:

&nbsp;       return ord(letter.upper()) - ord('A')



&nbsp;   def index\_to\_letter(self, index: int) -> str:

&nbsp;       return chr((index % 26) + ord('A'))



&nbsp;   def forward(self, letter: str) -> str:

&nbsp;       raise NotImplementedError



&nbsp;   def backward(self, letter: str) -> str:

&nbsp;       raise NotImplementedError





class Translator(Substitutor):

&nbsp;   """Defines a static forward mapping and auto-computed backward mapping."""



&nbsp;   def \_\_init\_\_(self, forward\_mapping: str):

&nbsp;       self.forward\_mapping = forward\_mapping.upper()



&nbsp;       # Build backward (reverse) mapping

&nbsp;       reverse = \[''] \* 26

&nbsp;       for i, ch in enumerate(self.forward\_mapping):

&nbsp;           idx = self.letter\_to\_index(ch)

&nbsp;           reverse\[idx] = self.index\_to\_letter(i)



&nbsp;       self.backward\_mapping = ''.join(reverse)



&nbsp;   def forward(self, letter: str) -> str:

&nbsp;       idx = self.letter\_to\_index(letter)

&nbsp;       return self.forward\_mapping\[idx]



&nbsp;   def backward(self, letter: str) -> str:

&nbsp;       idx = self.letter\_to\_index(letter)

&nbsp;       return self.backward\_mapping\[idx]





class Reflector(Translator):

&nbsp;   """Reflector uses same mapping for forward/backward."""

&nbsp;   pass





class Plugboard(Substitutor):

&nbsp;   """Swaps letters according to plugboard pairs."""



&nbsp;   def \_\_init\_\_(self, pairs=None):

&nbsp;       if pairs is None:

&nbsp;           pairs = \[]



&nbsp;       self.mapping = {chr(ord('A') + i): chr(ord('A') + i) for i in range(26)}



&nbsp;       for a, b in pairs:

&nbsp;           a, b = a.upper(), b.upper()

&nbsp;           self.mapping\[a] = b

&nbsp;           self.mapping\[b] = a



&nbsp;   def forward(self, letter: str) -> str:

&nbsp;       return self.mapping\[letter.upper()]



&nbsp;   def backward(self, letter: str) -> str:

&nbsp;       return self.mapping\[letter.upper()]





class Rotor(Translator):

&nbsp;   """Fully correct Enigma rotor."""



&nbsp;   def \_\_init\_\_(self, mapping: str, notch: str, ring\_setting: int = 1, offset: int = 1):

&nbsp;       super().\_\_init\_\_(mapping)



&nbsp;       self.notch = notch.upper()

&nbsp;       self.ring\_setting = (ring\_setting - 1) % 26

&nbsp;       self.offset = (offset - 1) % 26



&nbsp;   def at\_notch(self) -> bool:

&nbsp;       return self.index\_to\_letter(self.offset) == self.notch



&nbsp;   def step(self):

&nbsp;       self.offset = (self.offset + 1) % 26



&nbsp;   def forward(self, letter: str) -> str:

&nbsp;       i = self.letter\_to\_index(letter)

&nbsp;       s = (i + self.offset - self.ring\_setting) % 26

&nbsp;       w = self.forward\_mapping\[s]

&nbsp;       j = self.letter\_to\_index(w)

&nbsp;       o = (j - self.offset + self.ring\_setting) % 26

&nbsp;       return self.index\_to\_letter(o)



&nbsp;   def backward(self, letter: str) -> str:

&nbsp;       i = self.letter\_to\_index(letter)

&nbsp;       s = (i + self.offset - self.ring\_setting) % 26

&nbsp;       w = self.backward\_mapping\[s]

&nbsp;       j = self.letter\_to\_index(w)

&nbsp;       o = (j - self.offset + self.ring\_setting) % 26

&nbsp;       return self.index\_to\_letter(o)







class Enigma(Substitutor):

&nbsp;   """Full Enigma M3 machine with correct stepping + wiring."""



&nbsp;   def \_\_init\_\_(self, left: Rotor, middle: Rotor, right: Rotor, reflector: Reflector, plugboard: Plugboard):

&nbsp;       self.left = left

&nbsp;       self.middle = middle

&nbsp;       self.right = right

&nbsp;       self.reflector = reflector

&nbsp;       self.plugboard = plugboard



&nbsp;   def step\_rotors(self):

&nbsp;       # Double-stepping rule

&nbsp;       if self.middle.at\_notch():

&nbsp;           self.left.step()

&nbsp;           self.middle.step()

&nbsp;       elif self.right.at\_notch():

&nbsp;           self.middle.step()



&nbsp;       self.right.step()



&nbsp;   def encrypt\_letter(self, letter: str) -> str:

&nbsp;       if not letter.isalpha():

&nbsp;           return letter.upper()



&nbsp;       letter = letter.upper()



&nbsp;       # Step rotors

&nbsp;       self.step\_rotors()



&nbsp;       # Plugboard

&nbsp;       letter = self.plugboard.forward(letter)



&nbsp;       # Forward through rotors

&nbsp;       letter = self.right.forward(letter)

&nbsp;       letter = self.middle.forward(letter)

&nbsp;       letter = self.left.forward(letter)



&nbsp;       # Reflector

&nbsp;       letter = self.reflector.forward(letter)



&nbsp;       # Backward through rotors

&nbsp;       letter = self.left.backward(letter)

&nbsp;       letter = self.middle.backward(letter)

&nbsp;       letter = self.right.backward(letter)



&nbsp;       # Plugboard again

&nbsp;       letter = self.plugboard.backward(letter)



&nbsp;       return letter



&nbsp;   def encrypt(self, text: str) -> str:

&nbsp;       result = \[]

&nbsp;       for ch in text:

&nbsp;           if ch.isalpha():

&nbsp;               result.append(self.encrypt\_letter(ch))

&nbsp;           else:

&nbsp;               result.append(ch)

&nbsp;       return ''.join(result)





\# ---------------------- TEST ----------------------



if \_\_name\_\_ == "\_\_main\_\_":

&nbsp;   # From assignment

&nbsp;   ROTOR\_I = "EKMFLGDQVZNTOWYHXUSPAIBRCJ"

&nbsp;   ROTOR\_II = "AJDKSIRUXBLHWTMCQGZNPYFVOE"

&nbsp;   ROTOR\_III = "BDFHJLCPRTXVZNYEIWGAKMUSQO"



&nbsp;   NOTCH\_I = "Q"

&nbsp;   NOTCH\_II = "E"

&nbsp;   NOTCH\_III = "V"



&nbsp;   REFLECTOR\_B = "YRUHQSLDPXNGOKMIEBFZCWVJAT"



&nbsp;   plugboard = Plugboard(\[])



&nbsp;   r\_right = Rotor(ROTOR\_I, NOTCH\_I, ring\_setting=1, offset=1)

&nbsp;   r\_middle = Rotor(ROTOR\_II, NOTCH\_II, ring\_setting=1, offset=1)

&nbsp;   r\_left = Rotor(ROTOR\_III, NOTCH\_III, ring\_setting=1, offset=1)



&nbsp;   reflector = Reflector(REFLECTOR\_B)



&nbsp;   machine = Enigma(

&nbsp;       left=r\_left,

&nbsp;       middle=r\_middle,

&nbsp;       right=r\_right,

&nbsp;       reflector=reflector,

&nbsp;       plugboard=plugboard

&nbsp;   )



&nbsp;   result = machine.encrypt("ENIGMA")

&nbsp;   

&nbsp;   





def rotor\_first\_letter(rotor\_number: int) -> str:

&nbsp;   rotors = {

&nbsp;       1: "EKMFLGDQVZNTOWYHXUSPAIBRCJ",

&nbsp;       2: "AJDKSIRUXBLHWTMCQGZNPYFVOE",

&nbsp;       3: "BDFHJLCPRTXVZNYEIWGAKMUSQO",

&nbsp;       4: "ESOVPZJAYQUIRHXLNFTGKDCMWB",

&nbsp;       5: "VZBRGITYUPSDNHLXAWMJQOFECK",

&nbsp;   }

&nbsp;   return rotors\[rotor\_number]\[0]





def solve\_mapping\_task(text: str) -> str:

&nbsp;   mapping = {

&nbsp;       'Q': 'R',   # Rotor I

&nbsp;       'E': 'F',   # Rotor II

&nbsp;       'V': 'W',   # Rotor III

&nbsp;       'J': 'K',   # Rotor IV

&nbsp;       'Z': 'A'    # Rotor V

&nbsp;   }

&nbsp;   result = \[]

&nbsp;   for ch in text:

&nbsp;       result.append(mapping\[ch])

&nbsp;   return "".join(result)





print("Task 1 output:", solve\_mapping\_task("QEVJZ"))



print("\\n=== Task 2 Tests ===")



\# ----- Reflector Tests -----

print("\\nReflector B tests:")

reflector\_test = Reflector("YRUHQSLDPXNGOKMIEBFZCWVJAT")



tests\_reflector = \[

&nbsp;   ("B", "R"),

&nbsp;   ("E", "Q"),

&nbsp;   ("Q", "E"),

]



for inp, expected in tests\_reflector:

&nbsp;   out = reflector\_test.forward(inp)

&nbsp;   print(f"Input: {inp}  → Output: {out}  (Expected: {expected})")





\# ----- Plugboard Tests -----

print("\\nPlugboard tests:")



\# 1. empty plugboard

pb\_empty = Plugboard(\[])

print("Empty plugboard, Input T →", pb\_empty.forward("T"), "(Expected: T)")



\# 2. plugboard RY BU AS FZ

pb\_config = Plugboard(\[("R","Y"), ("B","U"), ("A","S"), ("F","Z")])

print("Config RY BU AS FZ, Input U →", pb\_config.forward("U"), "(Expected: B)")

print("Config RY BU AS FZ, Input B →", pb\_config.forward("B"), "(Expected: U)")



\# 3. large configuration from table

pb\_large = Plugboard(\[

&nbsp;   ("S","W"), ("A","Q"), ("N","P"), ("F","O"),

&nbsp;   ("V","Y"), ("U","X"), ("M","K"), ("C","L"), ("H","T"), ("Z","J")

])



print("Large config, Input U →", pb\_large.forward("U"), "(Expected: X)")

print("Large config, Input B →", pb\_large.forward("B"), "(Expected: B)")



def test\_task3():

&nbsp;   print("\\nTask 3 – Rotor functionality tests:\\n")



&nbsp;   # Rotor definitions

&nbsp;   ROTOR\_I = "EKMFLGDQVZNTOWYHXUSPAIBRCJ"

&nbsp;   ROTOR\_II = "AJDKSIRUXBLHWTMCQGZNPYFVOE"

&nbsp;   ROTOR\_III = "BDFHJLCPRTXVZNYEIWGAKMUSQO"

&nbsp;   ROTOR\_IV = "ESOVPZJAYQUIRHXLNFTGKDCMWB"

&nbsp;   ROTOR\_V = "VZBRGITYUPSDNHLXAWMJQOFECK"



&nbsp;   NOTCH\_I = "Q"

&nbsp;   NOTCH\_II = "E"

&nbsp;   NOTCH\_III = "V"

&nbsp;   NOTCH\_IV = "J"

&nbsp;   NOTCH\_V = "Z"



&nbsp;   # Helper for readable output

&nbsp;   def run\_rotor\_test(rotor\_name, mapping, notch, ring\_setting, offset, letter, direction):

&nbsp;       rotor = Rotor(mapping, notch, ring\_setting=ring\_setting, offset=offset)

&nbsp;       if direction == "forward":

&nbsp;           out = rotor.forward(letter)

&nbsp;       else:

&nbsp;           out = rotor.backward(letter)

&nbsp;       print(f"Rotor {rotor\_name} | Ring {ring\_setting} | Offset {offset} | "

&nbsp;             f"Input {letter} | {direction} → Output {out}")



&nbsp;   # Tests exactly like the table in the assignment PDF

&nbsp;   print("According to assignment examples:\\n")



&nbsp;   # Row 1

&nbsp;   run\_rotor\_test("I", ROTOR\_I, NOTCH\_I, ring\_setting=1, offset=1, letter="E", direction="forward")



&nbsp;   # Row 2

&nbsp;   run\_rotor\_test("I", ROTOR\_I, NOTCH\_I, ring\_setting=1, offset=4, letter="H", direction="forward")



&nbsp;   # Row 3

&nbsp;   run\_rotor\_test("I", ROTOR\_I, NOTCH\_I, ring\_setting=1, offset=23, letter="G", direction="forward")



&nbsp;   # Row 4 (highlighted yellow row in PDF)

&nbsp;   run\_rotor\_test("V", ROTOR\_V, NOTCH\_V, ring\_setting=5, offset=26, letter="P", direction="forward")



&nbsp;   # Reverse of row 4

&nbsp;   run\_rotor\_test("V", ROTOR\_V, NOTCH\_V, ring\_setting=5, offset=26, letter="X", direction="reverse")





\# Run Task 3 tests

test\_task3()



\#4 

def test\_task4():

&nbsp;   print("\\n==============================")

&nbsp;   print("        Task 4 Tests")

&nbsp;   print("==============================\\n")



&nbsp;   # Rotor mappings

&nbsp;   ROTORS = {

&nbsp;       "I":  "EKMFLGDQVZNTOWYHXUSPAIBRCJ",

&nbsp;       "II": "AJDKSIRUXBLHWTMCQGZNPYFVOE",

&nbsp;       "III":"BDFHJLCPRTXVZNYEIWGAKMUSQO",

&nbsp;       "IV": "ESOVPZJAYQUIRHXLNFTGKDCMWB",

&nbsp;       "V":  "VZBRGITYUPSDNHLXAWMJQOFECK",

&nbsp;   }



&nbsp;   NOTCHES = {

&nbsp;       "I": "Q",

&nbsp;       "II": "E",

&nbsp;       "III": "V",

&nbsp;       "IV": "J",

&nbsp;       "V": "Z",

&nbsp;   }



&nbsp;   def build\_machine(rotor\_order, ring\_settings, offsets, plug\_pairs):

&nbsp;       left\_name, mid\_name, right\_name = rotor\_order



&nbsp;       left  = Rotor(ROTORS\[left\_name],  NOTCHES\[left\_name],  ring\_settings\[0], offsets\[0])

&nbsp;       mid   = Rotor(ROTORS\[mid\_name],   NOTCHES\[mid\_name],   ring\_settings\[1], offsets\[1])

&nbsp;       right = Rotor(ROTORS\[right\_name], NOTCHES\[right\_name], ring\_settings\[2], offsets\[2])



&nbsp;       plugboard = Plugboard(plug\_pairs)

&nbsp;       reflector = Reflector("YRUHQSLDPXNGOKMIEBFZCWVJAT")



&nbsp;       return Enigma(left, mid, right, reflector, plugboard)



&nbsp;   # Helper to print case header

&nbsp;   def print\_case(rotors, rings, init\_offsets, final\_offsets, inp, out):

&nbsp;       print("--------------------------------------------")

&nbsp;       print(f"Rotors: {rotors\[0]}-{rotors\[1]}-{rotors\[2]}")

&nbsp;       print(f"Ring setting: {rings\[0]}-{rings\[1]}-{rings\[2]}")

&nbsp;       print(f"Initial ring offsets: {init\_offsets\[0]}-{init\_offsets\[1]}-{init\_offsets\[2]}")

&nbsp;       print(f"Final ring offsets:   {final\_offsets\[0]}-{final\_offsets\[1]}-{final\_offsets\[2]}")

&nbsp;       print(f"Input:  {inp}")

&nbsp;       print(f"Output: {out}")

&nbsp;       print("--------------------------------------------\\n")



&nbsp;   # ========== TASK 4 TEST CASES FROM PDF ==========



&nbsp;   ### CASE 1 ###

&nbsp;   rotors = ("I", "II", "III")

&nbsp;   rings = (1, 1, 1)

&nbsp;   offsets\_initial = (6, 4, 22)   # F-D-V

&nbsp;   machine = build\_machine(rotors, rings, offsets\_initial, plug\_pairs=\[])

&nbsp;   output = machine.encrypt("ENIGMA")

&nbsp;   print\_case(rotors, rings, ("F","D","V"), ("G","F","B"), "ENIGMA", output)



&nbsp;   ### CASE 2 ###

&nbsp;   rotors = ("I", "II", "III")

&nbsp;   rings = (1, 1, 1)

&nbsp;   offsets\_initial = (17, 5, 22)   # Q-E-V

&nbsp;   machine = build\_machine(rotors, rings, offsets\_initial, plug\_pairs=\[])

&nbsp;   output = machine.encrypt("KAXMNf".upper())

&nbsp;   print\_case(rotors, rings, ("Q","E","V"), ("R","F","B"), "KAXMNF", output)



&nbsp;   ### CASE 3 ###

&nbsp;   rotors = ("I", "II", "III")

&nbsp;   rings = (1, 1, 1)

&nbsp;   offsets\_initial = (24, 25, 5)   # X-E-Y

&nbsp;   machine = build\_machine(rotors, rings, offsets\_initial, plug\_pairs=\[])

&nbsp;   output = machine.encrypt("TURING")

&nbsp;   print\_case(rotors, rings, ("X","E","Y"), ("Y","F","E"), "TURING", output)



&nbsp;   ### CASE 4 ###

&nbsp;   rotors = ("I", "II", "IV")

&nbsp;   rings = (3, 8, 6)

&nbsp;   offsets\_initial = (19, 4, 9)   # S-D-I

&nbsp;   machine = build\_machine(rotors, rings, offsets\_initial, plug\_pairs=\[])

&nbsp;   output = machine.encrypt("PEACE")

&nbsp;   print\_case(rotors, rings, ("S","D","I"), ("T","F","N"), "PEACE", output)



&nbsp;   ### CASE 5 (with plugboard) ###

&nbsp;   rotors = ("I", "II", "IV")

&nbsp;   rings = (3, 8, 6)

&nbsp;   offsets\_initial = (19, 4, 9)

&nbsp;   machine = build\_machine(rotors, rings, offsets\_initial,

&nbsp;                           plug\_pairs=\[("A","T"), ("C","E"), ("R","L")])

&nbsp;   output = machine.encrypt("PEACE")

&nbsp;   print\_case(rotors, rings, ("S","D","I"), ("T","F","N"), "PEACE", output)



&nbsp;   ### CASE 6 – Big plugboard config ###

&nbsp;   rotors = ("II", "V", "IV")

&nbsp;   rings = (19, 9, 24)

&nbsp;   offsets\_initial = (3, 15, 14)   # C-O-N

&nbsp;   machine = build\_machine(rotors, rings, offsets\_initial,

&nbsp;       plug\_pairs=\[("Z","U"),("H","L"),("C","Q"),("W","A"),("O","P"),

&nbsp;                   ("Y","E"),("B","T"),("R","D"),("N","V"),("I","J")])

&nbsp;   output = machine.encrypt("DOR")

&nbsp;   print\_case(rotors, rings, ("C","O","N"), ("C","O","Q"), "DOR", output)





\# Run task 4 tests

test\_task4()



\#5

def task5():

&nbsp;   print("\\n====================")

&nbsp;   print("      TASK 5")

&nbsp;   print("====================\\n")



&nbsp;   # Rotors from assignment

&nbsp;   ROTOR\_II  = "AJDKSIRUXBLHWTMCQGZNPYFVOE"

&nbsp;   ROTOR\_V   = "VZBRGITYUPSDNHLXAWMJQOFECK"

&nbsp;   ROTOR\_IV  = "ESOVPZJAYQUIRHXLNFTGKDCMWB"



&nbsp;   NOTCH\_II  = "E"

&nbsp;   NOTCH\_V   = "Z"

&nbsp;   NOTCH\_IV  = "J"



&nbsp;   # Ring setting: 19-09-24

&nbsp;   ring\_settings = (19, 9, 24)



&nbsp;   # Plugboard from assignment

&nbsp;   pb\_pairs = \[

&nbsp;       ("Z","U"), ("H","L"), ("C","Q"), ("W","M"), ("O","A"),

&nbsp;       ("P","Y"), ("E","B"), ("T","R"), ("D","N"), ("V","I")

&nbsp;   ]

&nbsp;   plugboard = Plugboard(pb\_pairs)



&nbsp;   # Create machine with daily keys

&nbsp;   def make\_machine(offsets):

&nbsp;       left  = Rotor(ROTOR\_II, NOTCH\_II, ring\_setting=ring\_settings\[0], offset=offsets\[0])

&nbsp;       mid   = Rotor(ROTOR\_V,  NOTCH\_V,  ring\_setting=ring\_settings\[1], offset=offsets\[1])

&nbsp;       right = Rotor(ROTOR\_IV, NOTCH\_IV, ring\_setting=ring\_settings\[2], offset=offsets\[2])

&nbsp;       refl  = Reflector("YRUHQSLDPXNGOKMIEBFZCWVJAT")

&nbsp;       return Enigma(left, mid, right, refl, plugboard)



&nbsp;   # Messages to decrypt

&nbsp;   messages = \[

&nbsp;       "CON MLD",

&nbsp;       "RNYHP UMDPQ CUAQN LWWSP",

&nbsp;       "IARKC TIR3Q KCFPT OKRGO",

&nbsp;       "ZXALD RLPUH AUZ5O SZFSU",

&nbsp;       "GWNFF DZCUG VEXUU LQYX0",

&nbsp;       "TCYRP SYGGZ HQMAG PZDKC",

&nbsp;       "KGO3M MYMDD H",

&nbsp;   ]



&nbsp;   print("Daily rotors:  II – V – IV")

&nbsp;   print("Ring setting:  19-09-24")

&nbsp;   print("Plugboard:", " ".join(\["".join(p) for p in pb\_pairs]))

&nbsp;   print("\\nDecrypting...\\n")



&nbsp;   # Step 1: extract ground setting G from first group

&nbsp;   G\_cipher = messages\[0].split()\[1]

&nbsp;   print("Encrypted Ground Setting (G):", G\_cipher)



&nbsp;   machine\_G = make\_machine((1,1,1))  # offsets always start at AAA

&nbsp;   G\_plain = machine\_G.encrypt(G\_cipher)

&nbsp;   print("Decrypted Ground Setting (G):", G\_plain)



&nbsp;   # Step 2: use G as new offsets for decrypting message keys

&nbsp;   G\_offsets = \[ord(c)-64 for c in G\_plain]  # convert A→1, B→2, ...

&nbsp;   print("Ground offsets =", G\_offsets)



&nbsp;   # Step 3: decrypt every line using message key derived from G

&nbsp;   print("\\n----- FULL DECRYPTION -----")

&nbsp;   final\_output = \[]



&nbsp;   for line in messages:

&nbsp;       text = "".join(\[c for c in line if c.isalpha()])

&nbsp;       m = make\_machine(G\_offsets)

&nbsp;       plain = m.encrypt(text)

&nbsp;       final\_output.append((text, plain))

&nbsp;       print(f"{text}  →  {plain}")



&nbsp;   print("\\n---------- END TASK 5 ----------\\n")



task5()



\#בדיקה ל5

def build\_enigma(rotors, ring\_settings, offsets, plug\_pairs):

&nbsp;   """Helper function to build an Enigma machine with given settings."""

&nbsp;   # Rotor wirings

&nbsp;   ROTORS = {

&nbsp;       "I":   ("EKMFLGDQVZNTOWYHXUSPAIBRCJ", "Q"),

&nbsp;       "II":  ("AJDKSIRUXBLHWTMCQGZNPYFVOE", "E"),

&nbsp;       "III": ("BDFHJLCPRTXVZNYEIWGAKMUSQO", "V"),

&nbsp;       "IV":  ("ESOVPZJAYQUIRHXLNFTGKDCMWB", "J"),

&nbsp;       "V":   ("VZBRGITYUPSDNHLXAWMJQOFECK", "Z"),

&nbsp;   }



&nbsp;   # Create rotors (right → left)

&nbsp;   r\_right = Rotor(\*ROTORS\[rotors\[2]], ring\_setting=ring\_settings\[2], offset=offsets\[2])

&nbsp;   r\_middle = Rotor(\*ROTORS\[rotors\[1]], ring\_setting=ring\_settings\[1], offset=offsets\[1])

&nbsp;   r\_left = Rotor(\*ROTORS\[rotors\[0]], ring\_setting=ring\_settings\[0], offset=offsets\[0])



&nbsp;   reflector = Reflector("YRUHQSLDPXNGOKMIEBFZCWVJAT")

&nbsp;   plugboard = Plugboard(plug\_pairs)



&nbsp;   return Enigma(r\_left, r\_middle, r\_right, reflector, plugboard)







def round\_trip\_test(message, rotors, ring\_settings, offsets, plug\_pairs):

&nbsp;   print("\\n----- ROUND TRIP SYMMETRY TEST -----")



&nbsp;   # Step 1: Encrypt original message

&nbsp;   machine\_enc = build\_enigma(rotors, ring\_settings, offsets, plug\_pairs)

&nbsp;   encrypted = machine\_enc.encrypt(message)



&nbsp;   # Step 2: Decrypt by encrypting again with same settings

&nbsp;   machine\_dec = build\_enigma(rotors, ring\_settings, offsets, plug\_pairs)

&nbsp;   decrypted = machine\_dec.encrypt(encrypted)



&nbsp;   print("Original:  ", message)

&nbsp;   print("Encrypted: ", encrypted)

&nbsp;   print("Decrypted: ", decrypted)



&nbsp;   # Check symmetry

&nbsp;   if decrypted == message:

&nbsp;       print("\\nRESULT: ✔ Encryption is symmetric — machine is correct!\\n")

&nbsp;   else:

&nbsp;       print("\\nRESULT: ✘ Something is wrong — symmetry test failed!\\n")





\# --- Run symmetry test ---

round\_trip\_test(

&nbsp;   message="ENIGMA",

&nbsp;   rotors=("II", "V", "IV"),

&nbsp;   ring\_settings=(19, 9, 24),

&nbsp;   offsets=(4, 22, 12),   # Example offsets

&nbsp;   plug\_pairs=\[("Z","U"),("H","L"),("C","Q"),("W","M"),("O","A"),("P","Y"),

&nbsp;               ("E","B"),("T","R"),("D","N"),("V","I")]

)



\#6

import time



def benchmark\_task6(iterations=100000):

&nbsp;   print("\\n----- Task 6: Performance Benchmark -----\\n")

&nbsp;   start\_time = time.time()



&nbsp;   for \_ in range(iterations):

&nbsp;       # יצירת חלקי מכונת האניגמה

&nbsp;       plugboard = Plugboard(\[

&nbsp;           ("Z","U"), ("H","L"), ("C","Q"), ("W","M"), ("O","A"),

&nbsp;           ("P","Y"), ("E","B"), ("T","R"), ("D","N"), ("V","I")

&nbsp;       ])



&nbsp;       ROTOR\_II = "AJDKSIRUXBLHWTMCQGZNPYFVOE"

&nbsp;       ROTOR\_V  = "VZBRGITYUPSDNHLXAWMJQOFECK"

&nbsp;       ROTOR\_IV = "ESOVPZJAYQUIRHXLNFTGKDCMWB"



&nbsp;       NOTCH\_II = "E"

&nbsp;       NOTCH\_V  = "Z"

&nbsp;       NOTCH\_IV = "J"



&nbsp;       r\_right  = Rotor(ROTOR\_II, NOTCH\_II, ring\_setting=19, offset=19)

&nbsp;       r\_middle = Rotor(ROTOR\_V,  NOTCH\_V,  ring\_setting=9,  offset=9)

&nbsp;       r\_left   = Rotor(ROTOR\_IV, NOTCH\_IV, ring\_setting=24, offset=24)



&nbsp;       reflector = Reflector("YRUHQSLDPXNGOKMIEBFZCWVJAT")



&nbsp;       machine = Enigma(

&nbsp;           left=r\_left,

&nbsp;           middle=r\_middle,

&nbsp;           right=r\_right,

&nbsp;           reflector=reflector,

&nbsp;           plugboard=plugboard

&nbsp;       )



&nbsp;       # הצפנת הודעה קצרה לבחינת מהירות

&nbsp;       \_ = machine.encrypt("TEST")



&nbsp;   end\_time = time.time()

&nbsp;   total = end\_time - start\_time



&nbsp;   print(f"Iterations: {iterations}")

&nbsp;   print(f"Total time: {total:.3f} seconds")

&nbsp;   print(f"Average per iteration: {total/iterations:.8f} seconds")

&nbsp;   print("\\n-----------------------------------------\\n")



\# הפעלה

benchmark\_task6(100000)



